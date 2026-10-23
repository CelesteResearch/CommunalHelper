// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celeste.Graphics.Rendering.Drawing;
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
using System.Runtime.CompilerServices;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Mathematics.Easing;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Mods.CommunalHelper.Entities.Effects;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Core.Randomness;
using Player = Celeste.Simulation.RoomPlayerSession;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using StaticMover = Celeste.Simulation.Gameplay.Platforms.StaticMovers.SourceStaticMoverState;
using EventInstance = Celeste.Core.Audio.ISourceAudioEventReference;
using Coroutine = Celeste.Core.Scheduling.SimulationCoroutine;
using LightOcclude = Celeste.Simulation.Presentation.Lighting.LightOccludeState;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    [CustomEntity("CommunalHelper/DreamMoveBlock")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.DreamMoveBlock")]
    public partial class DreamMoveBlock : CustomDreamBlock, IRedirectableMoveBlock
    {
        private ISourceSpatialAudio SpatialAudio => ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio;
        private AtlasData Atlas => ((ISourceGameGraphicsEnvironment)Engine).GameGraphics.GameAtlas;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        public static DreamMoveBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) =>
            new DreamMoveBlock(level.EngineContext, data, offset, level.SolidWorldFactory);

        private readonly string playerTrackerKey, actorTrackerKey, solidTrackerKey, dreamTrackerKey;

        private Player CurrentPlayer =>
            ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<Player>(playerTrackerKey);

        private bool OnInterval(float interval) => ((ISourceScene)Scene).SourceScene.Clock.OnInterval(interval, Engine);

        private float Approach(float value, float target, float amount) => ScalarMath.Approach(value, target, amount,
            ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic);

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static float DirectionAngle(MoveBlockDirection direction) => direction switch
        {
            MoveBlockDirection.Left => (float)Math.PI, MoveBlockDirection.Up => -(float)Math.PI / 2f,
            MoveBlockDirection.Down => (float)Math.PI / 2f, _ => 0f
        };

        private static ParticleType[] dreamParticles;
        private int moveParticleIndex = 0;
        private int breakParticleIndex = 0;
        private int activateParticleIndex = 0;

        private const float Accel = 300f;
        private const float MoveSpeed = 60f;
        private const float FastMoveSpeed = 75f;

        private const float SteerSpeed = (float)Math.PI * 16f;
        private const float MaxAngle = (float)Math.PI / 4f;
        private const float NoSteerTime = 0.2f;

        private const float CrashResetTime = 0.1f;
        private const float CrashStartShakingTime = 0.15f;
        private readonly float crashTime;
        private readonly float regenTime;
        private readonly bool shakeOnCollision;

        private readonly float moveSpeed;

        public MoveBlockDirection direction;
        private float homeAngle;
        private readonly int angleSteerSign;
        internal Vector2 startPosition;

        private readonly GroupableMoveBlock groupable;

        private bool leftPressed;

        private bool rightPressed;

        private bool topPressed;

        private float speed;
        private float targetSpeed;

        private float angle;
        private float targetAngle;

        private Player noSquish;

        private List<Image> topButton = new();
        private List<Image> leftButton = new();
        private List<Image> rightButton = new();

        private readonly List<MTexture> arrows = new();

        private List<Image> body = new List<Image>();

        private Color fillColor = SourceHexColor.HexToColor("FFFFFF");
        private Color idleButtonsColor = SourceHexColor.HexToColor("FFFFFF");
        private Color idleArrowColor = SourceHexColor.HexToColor("FFFFFF");
        private Color idleWobbleLinesColor = SourceHexColor.HexToColor("FFFFFF");
        private Color movingButtonsColor = SourceHexColor.HexToColor("FFFFFF");
        private Color movingArrowColor = SourceHexColor.HexToColor("FFFFFF");
        private Color movingWobbleLinesColor = SourceHexColor.HexToColor("FFFFFF");
        private Color breakingWobbleLinesColor = SourceHexColor.HexToColor("FFFFFF");
        private Color breakingCrossColor = SourceHexColor.HexToColor("FFFFFF");

        private Color wobbleLineColor = SourceHexColor.HexToColor("FFFFFF");

        private Color currentArrowColor;

        private float flash;
        private readonly SoundSourceComponentState moveSfx;

        internal bool triggered;
        private float particleRemainder;

        private readonly Coroutine controller;
        private readonly bool noCollide;
        private readonly bool noCollideSteer;
        private readonly bool canSteer;

        private bool IsNoCollide => noCollide || noCollideSteer;

        private bool oneUseBroken;

        private readonly bool noDebris;

        internal static void InitializeParticles(ISourceEngineContext engine)
        {
            ParticleType particle = new(((IOriginalGameplayProcess)engine).ParticleTypes.MoveBlock.P_Move)
            {
                ColorMode = ParticleType.ColorModes.Choose
            };

            dreamParticles = new ParticleType[4];
            for (int i = 0; i < 4; i++)
                dreamParticles[i] = new ParticleType(particle);
        }

        public DreamMoveBlock(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds) : base(engine, data, offset, worlds)
        {
            var catalog = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog;
            playerTrackerKey = catalog.Find(typeof(Player)).RegistryKey;
            actorTrackerKey = catalog.Find(typeof(Celeste.Simulation.Gameplay.Actors.Foundation.SourceActorEntityState))
                .RegistryKey;
            solidTrackerKey = catalog.Find(typeof(SourceSolidEntityState)).RegistryKey;
            dreamTrackerKey = catalog.Find(typeof(Celeste.Game.Entities.Platforms.Dream.OriginalDreamBlockState))
                .RegistryKey;
            startPosition = Position;

            // Backwards Compatibility
            moveSpeed = data.Bool("fast") ? FastMoveSpeed : data.Float("moveSpeed", MoveSpeed);
            noCollide = data.Bool("noCollide");
            noCollideSteer = data.Bool("noCollideSteer", false);

            canSteer = data.Bool("canSteer");

            direction = data.Enum<MoveBlockDirection>("direction");
            switch (direction)
            {
                case MoveBlockDirection.Left:
                    angleSteerSign = -1;
                    break;
                case MoveBlockDirection.Up:
                    angleSteerSign = 1;
                    break;
                case MoveBlockDirection.Down:
                    angleSteerSign = -1;
                    break;
                default:
                    angleSteerSign = 1;
                    break;
            }

            homeAngle = targetAngle = angle = DirectionAngle(direction);

            crashTime = data.Float("crashTime", 0.15f);
            regenTime = data.Float("regenTime", 3f);
            shakeOnCollision = data.Bool("shakeOnCollision", true);

            noDebris = data.Bool("noDebris");

            if (data.Attr("idleButtonsColor", "FFFFFF") is not "FFFFFF")
                idleButtonsColor = SourceHexColor.HexToColor(data.Attr("idleButtonsColor"));

            if (data.Attr("idleArrowColor", "FFFFFF") is not "FFFFFF")
                idleArrowColor = SourceHexColor.HexToColor(data.Attr("idleArrowColor"));

            if (data.Attr("idleWobbleLinesColor", "FFFFFF") is not "FFFFFF")
                idleWobbleLinesColor = SourceHexColor.HexToColor(data.Attr("idleWobbleLinesColor"));

            if (data.Attr("movingButtonsColor", "FFFFFF") is not "FFFFFF")
                movingButtonsColor = SourceHexColor.HexToColor(data.Attr("movingButtonsColor"));

            if (data.Attr("movingArrowColor", "FFFFFF") is not "FFFFFF")
                movingArrowColor = SourceHexColor.HexToColor(data.Attr("movingArrowColor"));

            if (data.Attr("movingWobbleLinesColor", "FFFFFF") is not "FFFFFF")
                movingWobbleLinesColor = SourceHexColor.HexToColor(data.Attr("movingWobbleLinesColor"));

            if (data.Attr("breakingWobbleLinesColor", "FFFFFF") is not "FFFFFF")
                breakingWobbleLinesColor = SourceHexColor.HexToColor(data.Attr("breakingWobbleLinesColor"));

            if (data.Attr("breakingCrossColor", "FFFFFF") is not "FFFFFF")
                breakingCrossColor = SourceHexColor.HexToColor(data.Attr("breakingCrossColor"));

            currentArrowColor = idleArrowColor;

            arrows = Atlas.GetAtlasSubtextures("objects/CommunalHelper/dreamMoveBlock/arrow");
            Add(moveSfx = new SoundSourceComponentState(engine));
            Add(controller = new Coroutine(engine, Controller()));
            Add(new LightOcclude(0.5f));

            Add(groupable = new GroupableMoveBlock());

            Add(new MoveBlockRedirectable(this));
            int num = data.Width / 8;
            int num2 = data.Height / 8;
            MTexture mTexture = Atlas["objects/CommunalHelper/dreamMoveBlock/base"];
            MTexture mTexture2 = Atlas["objects/CommunalHelper/dreamMoveBlock/button"];
            if (canSteer && (direction == MoveBlockDirection.Left || direction == MoveBlockDirection.Right))
            {
                for (int i = 0; i < num; i++)
                {
                    int num3 = (i != 0) ? ((i < num - 1) ? 1 : 2) : 0;
                    AddImage(mTexture2.GetSubtexture(num3 * 8, 0, 8, 8), new Vector2(i * 8, -4f), 0f,
                        new Vector2(1f, 1f), topButton);
                }

                mTexture = Atlas["objects/CommunalHelper/dreamMoveBlock/base_h"];
            }
            else if (canSteer && (direction == MoveBlockDirection.Up || direction == MoveBlockDirection.Down))
            {
                for (int j = 0; j < num2; j++)
                {
                    int num4 = (j != 0) ? ((j < num2 - 1) ? 1 : 2) : 0;
                    AddImage(mTexture2.GetSubtexture(num4 * 8, 0, 8, 8), new Vector2(-4f, j * 8), (float)Math.PI / 2f,
                        new Vector2(1f, -1f), leftButton);
                    AddImage(mTexture2.GetSubtexture(num4 * 8, 0, 8, 8), new Vector2((num - 1) * 8 + 4, j * 8),
                        (float)Math.PI / 2f, new Vector2(1f, 1f), rightButton);
                }

                mTexture = Atlas["objects/CommunalHelper/dreamMoveBlock/base_v"];
            }

            for (int k = 0; k < num; k++)
            {
                for (int l = 0; l < num2; l++)
                {
                    int num5 = (k != 0) ? ((k < num - 1) ? 1 : 2) : 0;
                    int num6 = (l != 0) ? ((l < num2 - 1) ? 1 : 2) : 0;
                    AddImage(mTexture.GetSubtexture(num5 * 8, num6 * 8, 8, 8), new Vector2(k, l) * 8f, 0f,
                        new Vector2(1f, 1f), body);
                }
            }

            UpdateColors();
        }

        public override void BeginShatter()
        {
            if (groupable.State != GroupableMoveBlock.MovementState.Breaking)
                base.BeginShatter();
            oneUseBroken = true;
        }

        protected override void OneUseDestroy()
        {
            base.OneUseDestroy();
            Remove(controller);
            moveSfx.Stop();
        }

        public override void SetupCustomParticles(float canvasWidth, float canvasHeight)
        {
            base.SetupCustomParticles(canvasWidth, canvasHeight);
            if (PlayerHasDreamDash)
            {
                dreamParticles[0].Color = SourceHexColor.HexToColor("FFEF11");
                dreamParticles[0].Color2 = SourceHexColor.HexToColor("FF00D0");

                dreamParticles[1].Color = SourceHexColor.HexToColor("08a310");
                dreamParticles[1].Color2 = SourceHexColor.HexToColor("5fcde4");

                dreamParticles[2].Color = SourceHexColor.HexToColor("7fb25e");
                dreamParticles[2].Color2 = SourceHexColor.HexToColor("E0564C");

                dreamParticles[3].Color = SourceHexColor.HexToColor("5b6ee1");
                dreamParticles[3].Color2 = SourceHexColor.HexToColor("CC3B3B");
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    dreamParticles[i].Color = Color.LightGray * 0.5f;
                    dreamParticles[i].Color2 = Color.LightGray * 0.75f;
                }
            }
        }
    }
}
