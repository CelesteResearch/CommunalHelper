// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Collections.Generic;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedSolid
    {
        /// <summary>
        /// Optionnal function to do auto-tiling for the entire group, with specified textures.
        /// If is called more than once, the auto-tiling logic will not be performed, but will use the data from previous calls to pick the right tiles.
        /// </summary>
        /// <returns>
        ///     A list of Images, in case tile manipulation is needed.
        /// </returns>
        /// <param name="edges"> Split tiles from a 24x24 texture, for the edges and corners. </param>
        /// <param name="innerCorners"> Split tiles from a 16x16 texture, for the inside turns. </param>
        /// <param name="addAsComponent"> Whether all the tiles should be added as components to this entity, therefore rendered automatically. </param>
        public List<Image> AutoTile(MTexture[,] edges, MTexture[,] innerCorners, bool storeTiles = true,
            bool addAsComponent = true)
        {
            return AutoTile(edges, innerCorners, out _, storeTiles, addAsComponent);
        }

        public List<Image> AutoTile(MTexture[,] edges, MTexture[,] innerCorners, out List<Image> bgTiles,
            bool storeTiles = true, bool addAsComponent = true)
        {
            int tWidth = (int)((GroupBoundsMax.X - GroupBoundsMin.X) / 8);
            int tHeight = (int)((GroupBoundsMax.Y - GroupBoundsMin.Y) / 8);

            List<Image> res = [];
            bgTiles = [];

            if (!wasAutoTiled)
            {
                autoTileData = new AutoTileData[tWidth, tHeight];
            }

            for (int x = 1; x < tWidth + 1; x++)
            {
                for (int y = 1; y < tHeight + 1; y++)
                {
                    bool uncollidable = AllGroupTiles[x, y] && !GroupTiles[x, y];
                    bool[,] tiles = uncollidable ? AllGroupTiles : GroupTiles;
                    if (tiles[x, y])
                    {
                        Image tile = AddTile(tiles, x, y, GroupOffset, edges, innerCorners, storeTiles, wasAutoTiled);
                        res.Add(tile);

                        if (addAsComponent)
                        {
                            if (uncollidable)
                            {
                                BGRenderer.Add(tile);
                            }
                            else
                            {
                                Add(tile);
                            }
                        }

                        if (uncollidable)
                        {
                            bgTiles.Add(tile);
                            tile.Color = Color.Gray;
                        }
                    }
                }
            }

            wasAutoTiled = true;
            return res;
        }

        private Image AddTile(bool[,] tiles, int x, int y, Vector2 offset, MTexture[,] edges, MTexture[,] innerCorners,
            bool storeTiles, bool ignoreTilingLogic)
        {
            Image image;
            Vector2 tilePos = new(x - 1, y - 1);
            AutoTileData tileData;

            if (!ignoreTilingLogic)
            {
                bool up = tiles[x, y - 1];
                bool down = tiles[x, y + 1];
                bool left = tiles[x - 1, y];
                bool right = tiles[x + 1, y];

                bool upleft = tiles[x - 1, y - 1];
                bool upright = tiles[x + 1, y - 1];
                bool downleft = tiles[x - 1, y + 1];
                bool downright = tiles[x + 1, y + 1];

                image = AutoTileTexture(
                    (Sides)ToBitFlag(up, down, left, right),
                    (Corners)ToBitFlag(upleft, upright, downleft, downright),
                    edges, innerCorners, out tileData);
                autoTileData[(int)tilePos.X, (int)tilePos.Y] = tileData;
            }
            else
            {
                tileData = autoTileData[(int)tilePos.X, (int)tilePos.Y];
                image = new Image(
                    tileData.Type == TileType.InnerCorner
                        ? innerCorners[tileData.X, tileData.Y]
                        : edges[tileData.X, tileData.Y]);
            }

            image.Position = (tilePos * 8f) + offset;
            if (storeTiles)
            {
                Tiles.Add(image);
                switch (tileData.Type)
                {
                    default:
                    case TileType.Filler:
                        FillerTiles.Add(image);
                        break;
                    case TileType.Edge:
                        EdgeTiles.Add(image);
                        break;
                    case TileType.Corner:
                        CornerTiles.Add(image);
                        break;
                    case TileType.InnerCorner:
                        InnerCornerTiles.Add(image);
                        break;
                }
            }

            return image;
        }

        [Flags]
        private enum Sides
        {
            Up = 1,
            Down = 2,
            Left = 4,
            Right = 8,
            All = Up | Down | Left | Right
        }

        [Flags]
        private enum Corners
        {
            UpLeft = 1,
            UpRight = 2,
            DownLeft = 4,
            DownRight = 8,
            All = UpLeft | UpRight | DownLeft | DownRight
        }

        private static Image AutoTileTexture(Sides sides, Corners corners, MTexture[,] edges, MTexture[,] innerCorners,
            out AutoTileData data)
        {
            data = sides == Sides.All
                ? corners switch
                {
                    Corners.All ^ Corners.UpLeft => new AutoTileData(0, 0, TileType.InnerCorner),
                    Corners.All ^ Corners.UpRight => new AutoTileData(1, 0, TileType.InnerCorner),
                    Corners.All ^ Corners.DownLeft => new AutoTileData(0, 1, TileType.InnerCorner),
                    Corners.All ^ Corners.DownRight => new AutoTileData(1, 1, TileType.InnerCorner),
                    _ => new AutoTileData(1, 1, TileType.Filler)
                }
                : sides switch
                {
                    Sides.All ^ Sides.Up => new AutoTileData(1, 0, TileType.Edge),
                    Sides.All ^ Sides.Down => new AutoTileData(1, 2, TileType.Edge),
                    Sides.All ^ Sides.Left => new AutoTileData(0, 1, TileType.Edge),
                    Sides.All ^ Sides.Right => new AutoTileData(2, 1, TileType.Edge),
                    Sides.Down | Sides.Right => new AutoTileData(0, 0, TileType.Corner),
                    Sides.Down | Sides.Left => new AutoTileData(2, 0, TileType.Corner),
                    Sides.Up | Sides.Right => new AutoTileData(0, 2, TileType.Corner),
                    Sides.Up | Sides.Left => new AutoTileData(2, 2, TileType.Corner),
                    _ => new AutoTileData(1, 1, TileType.Filler)
                };

            return new Image(
                data.Type == TileType.InnerCorner ? innerCorners[data.X, data.Y] : edges[data.X, data.Y]);
        }
    }
}
