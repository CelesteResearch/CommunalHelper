// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Gameplay.Collision;
using Celeste.Physics.Collision;
using Celeste.Mods.CommunalHelper.Content.Stations;
using Celeste.Mods.CommunalHelper.Runtime.Hazards;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using SoundSource = Celeste.Simulation.Audio.Sources.SoundSourceComponentState;
using Player = Celeste.Simulation.RoomPlayerSession;
using DashCollisionResults = Celeste.Simulation.Gameplay.Player.Collision.Surfaces.DashCollisionResult;
using Node = Celeste.Mods.CommunalHelper.Entities.Stations.StationBlockTrack.Node;

namespace Celeste.Mods.CommunalHelper.Entities.Stations
{
    [CustomEntity("CommunalHelper/StationBlock")]
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.StationBlock")]
    public partial class StationBlock : SourceSolidEntityState
    {
        [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.StationBlock+Themes")]
        public enum Themes
        {
            Normal,
            Moon
        }

        private readonly MTexture[,] tileSlices;
        private MTexture[,] blockTiles;
        private readonly MTexture[,] buttonTiles;
        private readonly Sprite arrowSprite;

        private readonly bool allowWavedash, allowWavedashBottom;

        public MTexture CustomNode = null, CustomTrackV = null, CustomTrackH = null;

        private readonly ParticleType p_sparks;

        private ArrowDir arrowDir;
        private float Percent = 0f;

        [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.StationBlock+ArrowDir")]
        public enum ArrowDir
        {
            Up,
            Right,
            Down,
            Left,
        }

        private bool IsMoving = false;
        private Vector2 scale = Vector2.One;

        private Vector2 MoveDir;
        public bool ReverseControls = false;

        private Vector2 offset;
        private Vector2 hitOffset;
        private readonly SoundSource Sfx;
        public Themes Theme = Themes.Moon;

        private static readonly Color activatedButton = SourceHexColor.HexToColor("f25eff");
        private static readonly Color deactivatedButton = SourceHexColor.HexToColor("5bf75b");

        private Color buttonColor, buttonPressedColor;
        private float colorLerp, bottomColorLerp;

        private readonly float speedFactor = 1f;

        private readonly float volume = 1f;

        public bool IsAttachedToTrack = false;
        public Node CurrentNode = null;
        private Node nextNode;
        private StationBlockTrack currentTrack;

        private readonly bool dashCornerCorrection;

        public static StationBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset, level.SolidWorldFactory);

        public StationBlock(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : base(data.Position + offset, data.Width, data.Height, true, worlds.Create(data.Position + offset, true))
        {
            Depth = -9999;
            OwnedComponents.Add(new LightOccludeState());

            this.offset = new Vector2(Width, Height) / 2f;
            allowWavedash = data.Bool("allowWavedash", false);
            allowWavedashBottom = data.Bool("allowWavedashBottom", false);
            speedFactor = ScalarMath.Clamp(data.Float("speedFactor", 1f), .1f, 2f);
            volume = ScalarMath.Clamp(data.Float("volume", 1f), 0f, 1f);

            buttonColor = ReadColor(data, "wavedashButtonColor", deactivatedButton);
            buttonPressedColor = ReadColor(data, "wavedashButtonPressedColor", activatedButton);

            dashCornerCorrection = data.Bool("dashCornerCorrection", false);

            int minSize = (int)Math.Min(Width, Height);
            string size = minSize <= 16 ? "small" : minSize <= 24 ? "medium" : "big";
            string block = "objects/CommunalHelper/stationBlock/blocks/";
            string sprite;
            ReverseControls = data.Attr("behavior", "Pulling") == "Pushing";
            Theme = data.Enum<Themes>("theme");

            string customBlockPath = data.Attr("customBlockPath").Trim();
            string customArrowPath = data.Attr("customArrowPath").Trim();
            string customTrackPath = data.Attr("customTrackPath").Trim();

            switch (Theme)
            {
                default:
                case Themes.Normal:
                    if (ReverseControls)
                    {
                        block += "alt_block";
                        sprite = size + "AltStationBlockArrow";
                    }
                    else
                    {
                        block += "block";
                        sprite = size + "StationBlockArrow";
                    }

                    break;

                case Themes.Moon:
                    Theme = Themes.Moon;
                    if (ReverseControls)
                    {
                        block += "alt_moon_block";
                        sprite = size + "AltMoonStationBlockArrow";
                    }
                    else
                    {
                        block += "moon_block";
                        sprite = size + "MoonStationBlockArrow";
                    }

                    break;
            }

            if (allowWavedash && allowWavedashBottom)
                block += "_button_both";
            else if (allowWavedashBottom)
                block += "_button_bottom";
            else if (allowWavedash)
                block += "_button";

            MTexture customBlock = null;
            Sprite customArrow = null;

            p_sparks = Theme == Themes.Normal
                ? ((IOriginalGameplayProcess)Engine).ParticleTypes.ZipMover.P_Sparks
                : (ReverseControls
                    ? StationResources.For(Engine).P_PurpleSparks
                    : StationResources.For(Engine).P_BlueSparks);

            if (!string.IsNullOrEmpty(customBlockPath))
            {
                customBlock = GameAtlas["objects/" + customBlockPath];
                p_sparks = ((IOriginalGameplayProcess)Engine).ParticleTypes.ZipMover.P_Sparks;
            }

            if (!string.IsNullOrEmpty(customArrowPath))
            {
                customArrow = LookForCustomSprite("objects/" + customArrowPath, size);
            }

            if (!string.IsNullOrEmpty(customTrackPath))
            {
                CustomNode = GameAtlas["objects/" + customTrackPath + "/node"];
                CustomTrackV = GameAtlas["objects/" + customTrackPath + "/trackv"];
                CustomTrackH = GameAtlas["objects/" + customTrackPath + "/trackh"];
            }

            tileSlices = new MTexture[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    tileSlices[i, j] = (customBlock ?? GameAtlas[block]).GetSubtexture(i * 8, j * 8, 8, 8);
                }
            }

            /*
             * the first dimension here represents the part of the button (left, middle, right),
             * the second (0 or 1) will represent whether we want to draw the outline or the front.
             */
            buttonTiles = new MTexture[3, 2];
            for (int i = 0; i < 3; i++)
            {
                buttonTiles[i, 0] = GameAtlas["objects/CommunalHelper/stationBlock/button_outline"]
                    .GetSubtexture(i * 8, 0, 8, 8);
                buttonTiles[i, 1] = GameAtlas["objects/CommunalHelper/stationBlock/button"]
                    .GetSubtexture(i * 8, 0, 8, 8);
            }

            GenerateTiles();

            arrowSprite = customArrow ?? StationResources.For(Engine).SpriteBank.Create(sprite);
            arrowDir = ArrowDir.Up;
            arrowSprite.Position = new Vector2(Width / 2, Height / 2);
            OwnedComponents.Add(arrowSprite);

            SurfaceSoundIndex = 7;
            OwnedComponents.Add(new SimulationCoroutine(Engine, Sequence()));
            OwnedComponents.Add(Sfx = new SoundSource(Engine));
            Sfx.Position = this.offset;
            OnDashCollide = OnDashed;
        }

        private ISourceEngineContext Engine => SourceWorld.EngineContext;
        private AtlasData GameAtlas => ((ISourceGameGraphicsEnvironment)Engine).GameGraphics.GameAtlas;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;
        private Celeste.Scene.Scheduling.SceneClock SceneClock => ((ISourceScene)Scene).SourceScene.Clock;

        private static Color ReadColor(EntityData data, string key, Color fallback)
            => data.Values.ContainsKey(key) ? SourceHexColor.HexToColor(data.Attr(key)) : fallback;

        private bool PlayerCollidesWithSpikes(Player player)
            => SourceSceneCollisionQueries.Check(player, Level.Scheduler.Entities.GetTrackedEntities("Celeste.Spikes"));

        private Color MixColor(Color first, Color second, float amount)
        {
            amount = ScalarMath.Clamp(amount, 0f, 1f);
            var arithmetic = ((ISourceScalarEnvironment)Engine).ScalarArithmetic;
            return new Color((int)arithmetic.Lerp(first.R, second.R, amount),
                (int)arithmetic.Lerp(first.G, second.G, amount),
                (int)arithmetic.Lerp(first.B, second.B, amount),
                (int)arithmetic.Lerp(first.A, second.A, amount));
        }

        public override void Update()
        {
            base.Update();
            colorLerp = ScalarMath.Approach(colorLerp, 0f, 1.25f * Engine.DeltaTime);
            bottomColorLerp = ScalarMath.Approach(bottomColorLerp, 0f, 1.25f * Engine.DeltaTime);

            scale.X = ScalarMath.Approach(scale.X, 1f, Engine.DeltaTime * 4f);
            scale.Y = ScalarMath.Approach(scale.Y, 1f, Engine.DeltaTime * 4f);
            hitOffset.X = ScalarMath.Approach(hitOffset.X, 0f, Engine.DeltaTime * 15f);
            hitOffset.Y = ScalarMath.Approach(hitOffset.Y, 0f, Engine.DeltaTime * 15f);

            arrowSprite.Scale = scale;
            arrowSprite.Position = hitOffset + offset;

            if (CurrentNode is not null && CurrentNode.PushForce != Vector2.Zero && !IsMoving)
            {
                Smash(CurrentNode.PushForce, force: true);
            }
        }
    }
}
