// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using Celeste.Graphics.Content.Atlas;

namespace Celeste.Mods.CommunalHelper.Content
{
    public sealed class ConnectedMoveBlockTextures
    {
        public AtlasEntry[,] Edges { get; } = new AtlasEntry[3, 3];
        public AtlasEntry[,] InnerCorners { get; } = new AtlasEntry[2, 2];
        public List<AtlasEntry> Arrows { get; } = [];
        public List<AtlasEntry> Debris { get; } = [];
        public AtlasEntry BreakingIcon { get; private set; }

        public ConnectedMoveBlockTextures(AtlasData atlas)
            => Reload(atlas);

        public void Reload(AtlasData atlas)
        {
            AtlasEntry tileset = atlas["objects/CommunalHelper/connectedMoveBlock/tileset"];
            Arrows.Clear();
            Arrows.AddRange(atlas.GetAtlasSubtextures("objects/CommunalHelper/connectedMoveBlock/arrow"));
            BreakingIcon = atlas["objects/CommunalHelper/connectedMoveBlock/x"];
            Debris.Clear();
            Debris.AddRange(atlas.GetAtlasSubtextures("objects/CommunalHelper/connectedMoveBlock/debris"));
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++)
                Edges[x, y] = tileset.GetSubtexture(x * 8, y * 8, 8, 8);
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                InnerCorners[x, y] = tileset.GetSubtexture(24 + x * 8, y * 8, 8, 8);
        }
    }
}
