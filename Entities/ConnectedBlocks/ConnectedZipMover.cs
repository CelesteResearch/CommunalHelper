// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Entities.Platforms.ZipMovers.Motion;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Platforms.ZipMovers;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    [CustomEntity("CommunalHelper/ConnectedZipMover")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.ConnectedZipMover")]
    public partial class ConnectedZipMover : ConnectedSolid
    {
        private ISourceEngineContext Engine => SourceWorld.EngineContext;
        private AtlasData GameAtlas => ((ISourceGameGraphicsEnvironment)Engine).GameGraphics.GameAtlas;
        private static Vector2 Perpendicular(Vector2 value) => new(-value.Y, value.X);

        private static Rectangle CameraBounds(OriginalLevelSceneRoot level) => new((int)level.Camera.Left,
            (int)level.Camera.Top, (int)level.Camera.Right - (int)level.Camera.Left,
            (int)level.Camera.Bottom - (int)level.Camera.Top);

        private PathRenderer pathRenderer;

        [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.ConnectedZipMover+Themes")]
        public enum Themes
        {
            Normal,
            Moon,
            Cliffside
        }

        public readonly Themes theme;

        private readonly MTexture[,] edges = new MTexture[3, 3];
        private readonly MTexture[,] innerCorners = new MTexture[2, 2];
        private readonly Sprite streetlight;
        private readonly List<MTexture> innerCogs;
        private readonly MTexture temp = new();

        private readonly bool drawBlackBorder;

        private readonly SoundSourceComponentState sfx;
        private readonly BloomPointState bloom;

        private readonly bool permanent;
        private readonly bool waits;
        private readonly bool ticking;
        private readonly SimulationCoroutine seq;

        private readonly string sfxPath;
        private Color backgroundColor;

        private readonly Vector2[] nodes;

        private readonly Color ropeColor, ropeLightColor;
        private readonly MTexture cog;

        private float percent;

        public static ConnectedZipMover Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(data, offset, level.SolidWorldFactory);

        public ConnectedZipMover(EntityData data, Vector2 offset, ISourceSolidWorldFactory worlds)
            : this(data.Position + offset, data.Width, data.Height, data.NodesWithPosition(offset),
                data.Enum("theme", Themes.Normal),
                data.Bool("permanent"),
                data.Bool("waiting"),
                data.Bool("ticking"),
                data.Attr("customSkin").Trim(),
                data.Attr("colors").Trim(),
                data.Bool("drawBlackBorder", true),
                data.Attr("customBlockTexture").Trim(), worlds.Create(data.Position + offset, false))
        {
        }

        public ConnectedZipMover(Vector2 position, int width, int height, Vector2[] nodes, Themes theme, bool permanent,
            bool waits, bool ticking, string customSkin, string colors, bool drawBlackBorder,
            string legacyCustomTexture,
            ISourceSolidWorld world)
            : base(position, width, height, false, world)
        {
            Depth = -9999;
            SurfaceSoundIndex = 7;

            this.nodes = nodes;

            this.theme = theme;
            this.permanent = permanent;
            this.waits = waits;
            this.ticking = ticking;

            // set default theming attributes
            string themeName;
            switch (this.theme)
            {
                default:
                case Themes.Normal:
                    themeName = "normal";
                    sfxPath = "normal";
                    this.drawBlackBorder = true;
                    backgroundColor = Color.Black;
                    break;

                case Themes.Moon:
                    themeName = "moon";
                    sfxPath = "moon";
                    this.drawBlackBorder = false;
                    backgroundColor = Color.Black;
                    break;

                case Themes.Cliffside:
                    themeName = "cliffside";
                    sfxPath = "normal";
                    this.drawBlackBorder = true;
                    backgroundColor = SourceHexColor.HexToColor("171018");
                    break;
            }

            ropeColor = SourceHexColor.HexToColor("663931");
            ropeLightColor = SourceHexColor.HexToColor("9b6157");

            // set texture paths
            string light, innerCog, block = null, corners = null, tileset = null;
            if (!string.IsNullOrEmpty(customSkin))
            {
                light = customSkin + "/light";
                innerCog = customSkin + "/innercog";
                cog = GameAtlas[customSkin + "/cog"];

                // prefer combined block and inner corner textures over separate
                if (GameAtlas.Has(customSkin + "/tileset"))
                {
                    tileset = customSkin + "/tileset";
                }
                else
                {
                    block = customSkin + "/block";
                    corners = customSkin + "/innerCorners";
                }

                // override colors + border if we have a custom skin
                if (!string.IsNullOrEmpty(colors))
                {
                    // Comma seperated list of colors
                    // First is background color, second is main rope color, third is light rope color
                    string[] colorList = colors.Split(',');
                    if (colorList.Length > 0)
                        backgroundColor = SourceColorParsing.HexToPackedColorWithAlpha(colorList[0]);
                    if (colorList.Length > 1)
                        ropeColor = SourceColorParsing.HexToPackedColorWithAlpha(colorList[1]);
                    if (colorList.Length > 2)
                        ropeLightColor = SourceColorParsing.HexToPackedColorWithAlpha(colorList[2]);
                }

                this.drawBlackBorder = drawBlackBorder;
            }
            else
            {
                string themePath = "objects/CommunalHelper/connectedZipMover/" + themeName;
                light = themePath + "/light";
                innerCog = themePath + "/innercog";
                cog = GameAtlas[themePath + "/cog"];
                tileset = themePath + "/tileset";
            }

            // setup tileset
            if (tileset is not null)
            {
                // non-legacy, combined block and inner corner textures
                Tuple<MTexture[,], MTexture[,]> customTiles = SetupCustomTileset(Engine, tileset, false);
                edges = customTiles.Item1;
                innerCorners = customTiles.Item2;
            }
            else if (string.IsNullOrEmpty(legacyCustomTexture))
            {
                // non-legacy, separate block and inner corner textures
                for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    edges[i, j] = GameAtlas[block].GetSubtexture(i * 8, j * 8, 8, 8);
                for (int i = 0; i < 2; i++)
                for (int j = 0; j < 2; j++)
                    innerCorners[i, j] = GameAtlas[corners].GetSubtexture(i * 8, j * 8, 8, 8);
            }
            else
            {
                // legacy
                Tuple<MTexture[,], MTexture[,]> customTiles = SetupCustomTileset(Engine, legacyCustomTexture, true);
                edges = customTiles.Item1;
                innerCorners = customTiles.Item2;
            }

            innerCogs = GameAtlas.GetAtlasSubtextures(innerCog);

            streetlight = new Sprite(Engine, GameAtlas, light)
            {
                Active = false
            };
            streetlight.Add("frames", "", 1f);
            streetlight.Play("frames");
            streetlight.SetAnimationFrame(1);
            streetlight.Position = new Vector2((Width / 2f) - (streetlight.Width / 2f), 0f);

            Add(seq = new SimulationCoroutine(Engine, Sequence()));

            Add(sfx = ((ISourceSoundSourceEnvironment)Engine).SoundSources.Create());
            sfx.Position = ((ISourceVectorEnvironment)Engine).VectorArithmetic.Divide(new Vector2(Width, Height), 2f);
            Add(bloom = new BloomPointState(1f, 6f) { Position = new Vector2(Width / 2f, 4f) });
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            AutoTile(edges, innerCorners);

            Add(streetlight);

            AddLightOccluders(1f);
        }

        public override void Added(object scene)
        {
            base.Added(scene);

            ((ISourceScene)scene).SourceScene.QueueSourceEntity(pathRenderer = new(this, nodes, ropeColor,
                ropeLightColor));
        }

        public override void Removed(object scene)
        {
            ((ISourceScene)scene).SourceScene.RemoveEntity(pathRenderer);
            pathRenderer = null;
            base.Removed(scene);
        }

        public override void Update()
        {
            base.Update();

            bloom.Visible = streetlight.CurrentAnimationFrame != 0;
            bloom.Y = theme switch
            {
                Themes.Normal => streetlight.CurrentAnimationFrame * 3,
                Themes.Moon => 9,
                Themes.Cliffside => 5,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        internal static void InitializeTextures(ISourceEngineContext engine)
        {
            // people rely on `objects/CommunalHelper/connectedZipMover/cliffside/block` existing apparently? i hate modding
            // i dont want to clutter the graphics folders so let's be evil. if they are evil to us we can be evil back
            ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas
                    ["objects/CommunalHelper/connectedZipMover/cliffside/block"] =
                ((ISourceGameGraphicsEnvironment)engine)
                .GameGraphics.GameAtlas["objects/CommunalHelper/connectedZipMover/cliffside/tileset"]
                .GetSubtexture(0, 0, 24, 24);
        }
    }
}
