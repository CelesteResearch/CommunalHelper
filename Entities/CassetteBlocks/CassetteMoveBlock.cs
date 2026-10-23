// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Framework;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Entities.Platforms.SwapBlocks.Motion;
using Celeste.Game.Entities.Platforms.SwapBlocks.Rendering;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.CommunalHelper.Presentation.Colors;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Dash;
using Color = Celeste.Core.Display.PackedColor;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Celeste.Simulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Graphics.Rendering;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Celeste.Mods.CommunalHelper.Physics;
using Directions = Celeste.Simulation.Gameplay.Platforms.MoveBlock.MoveBlockDirection;
using MoveBlockDebris = Celeste.Mods.CommunalHelper.Entities.Effects.MoveBlockDebris;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    [CustomEntity("CommunalHelper/CassetteMoveBlock")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CassetteMoveBlock")]
    public partial class CassetteMoveBlock : CustomCassetteBlock, IRedirectableMoveBlock
    {
        private const float Accel = 300f;
        private const float MoveSpeed = 60f;
        private const float FastMoveSpeed = 75f;
        private const float SteerSpeed = (float)Math.PI * 16f;
        private const float NoSteerTime = 0.2f;
        private const float CrashResetTime = 0.1f;
        private const float CrashStartShakingTime = 0.15f;
        private readonly float crashTime;
        private readonly float regenTime;
        private readonly bool shakeOnCollision;

        private readonly GroupableMoveBlock groupable;
        private readonly MoveBlockRedirectable redirectable;
        private float redirectMoveSpeed;
        private ISourceEngineContext Engine => SourceWorld.EngineContext;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        public new static CassetteMoveBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset,
            OriginalLevelEntityData data) =>
            new(data, offset, new MapEntityId(room.Name, data.ID), level.SolidWorldFactory);

        private readonly float moveSpeed;
        public Directions Direction;
        private float homeAngle;
        private Vector2 startPosition;
        private float speed;
        private float targetSpeed;
        private float angle;
        private float targetAngle;

        private RoomPlayerSession noSquish;

        private Image arrow;
        private Image arrowPressed;
        private Image arrowHighlight;
        private Image arrowHighlightPressed;
        private Image cross;
        private Image crossPressed;
        private Image crossHighlight;
        private Image crossHighlightPressed;

        private float flash;
        private readonly SoundSourceComponentState moveSfx;
        private bool triggered;
        private float particleRemainder;

        private readonly ParticleType P_Activate;
        private readonly ParticleType P_Move;
        private readonly ParticleType P_MovePressed;
        private readonly ParticleType P_Break;
        private readonly ParticleType P_BreakPressed;

        private readonly bool noDebris;

        // when moving, ignore these solid types
        protected readonly Type[] ignores;

        public CassetteMoveBlock(Vector2 position, MapEntityId id, int width, int height, Directions direction,
            float moveSpeed, int index, float tempo, bool oldConnectionBehavior, Color? overrideColor,
            string spritePath,
            float sideAlpha, bool held, float crashTime, float regenTime, bool shakeOnCollision, bool noDebris,
            Type[] ignores, ISourceSolidWorld world)
            : base(position, id, width, height, index, tempo, true, oldConnectionBehavior, dynamicHitbox: true,
                overrideColor, spritePath, sideAlpha, held, world)
        {
            playerTrackerKey = ((ISourceSceneCatalogEnvironment)Engine).SceneCatalog.Find(typeof(RoomPlayerSession))
                .RegistryKey;
            actorTrackerKey = ((ISourceSceneCatalogEnvironment)Engine).SceneCatalog.Find(typeof(SourceActorEntityState))
                .RegistryKey;
            startPosition = position;
            Direction = direction;
            this.moveSpeed = moveSpeed;

            homeAngle = targetAngle = angle = DirectionAngle(direction);

            this.crashTime = crashTime;
            this.regenTime = regenTime;
            this.shakeOnCollision = shakeOnCollision;

            OwnedComponents.Add(moveSfx = new SoundSourceComponentState(Engine));
            OwnedComponents.Add(new SimulationCoroutine(Engine, Controller()));
            OwnedComponents.Add(groupable = new GroupableMoveBlock { AllowGroupingWhenNoncollidable = true });

            P_Activate = new ParticleType(((IOriginalGameplayProcess)Engine).ParticleTypes.MoveBlock.P_Activate)
                { Color = color };
            P_Move = new ParticleType(((IOriginalGameplayProcess)Engine).ParticleTypes.MoveBlock.P_Move)
                { Color = color };
            P_MovePressed = new ParticleType(((IOriginalGameplayProcess)Engine).ParticleTypes.MoveBlock.P_Move)
                { Color = pressedColor };
            P_Break = new ParticleType(((IOriginalGameplayProcess)Engine).ParticleTypes.MoveBlock.P_Break)
                { Color = color };
            P_BreakPressed = new ParticleType(((IOriginalGameplayProcess)Engine).ParticleTypes.MoveBlock.P_Break)
                { Color = pressedColor };

            this.noDebris = noDebris;

            redirectMoveSpeed = moveSpeed;
            OwnedComponents.Add(redirectable = new MoveBlockRedirectable(this));

            this.ignores = ignores;
        }

        public CassetteMoveBlock(EntityData data, Vector2 offset, MapEntityId id, ISourceSolidWorldFactory worlds)
            : this(data, offset, id, worlds.Create(data.Position + offset, false))
        {
        }

        private CassetteMoveBlock(EntityData data, Vector2 offset, MapEntityId id, ISourceSolidWorld world)
            : this(data.Position + offset, id, data.Width, data.Height, EntityEnum(data),
                data.Bool("fast") ? FastMoveSpeed : data.Float("moveSpeed", MoveSpeed), data.Int("index"),
                data.Float("tempo", 1f), data.Bool("oldConnectionBehavior", true),
                HexColorNullable(data, "customColor"),
                data.Attr("spritePath", ""), data.Float("sideAlpha", 1f), data.Bool("held"),
                data.Float("crashTime", 0.15f),
                data.Float("regenTime", 3f), data.Bool("shakeOnCollision", true), data.Bool("noDebris"),
                ResolveIgnoredTypes(world.EngineContext, data.Attr("ignore", "")), world)
        {
        }

        public override void Awake(object scene)
        {
            int index = (int)Math.Floor(((0f - angle + ((float)Math.PI * 2f)) % ((float)Math.PI * 2f) /
                ((float)Math.PI * 2f) * 8f) + 0.5f);
            arrow = new Image(
                GameAtlas.GetAtlasSubtextures(SuffixFromSpritePathOrDefault("/arrow",
                    "objects/CommunalHelper/cassetteMoveBlock/arrow"))[index]);
            arrowPressed = new Image(GameAtlas.GetAtlasSubtextures(SuffixFromSpritePathOrDefault("/arrowPressed",
                "objects/CommunalHelper/cassetteMoveBlock/arrowPressed"))[index]);
            arrowHighlight = new Image(GameAtlas.GetAtlasSubtextures(SuffixFromSpritePathOrDefault("/arrowHighlight",
                "objects/CommunalHelper/cassetteMoveBlock/arrowHighlight"))[index]);
            arrowHighlightPressed = new Image(GameAtlas.GetAtlasSubtextures(
                SuffixFromSpritePathOrDefault("/arrowHighlightPressed",
                    "objects/CommunalHelper/cassetteMoveBlock/arrowHighlightPressed"))[index]);
            cross = new Image(
                GameAtlas[SuffixFromSpritePathOrDefault("/x", "objects/CommunalHelper/cassetteMoveBlock/x")]);
            crossPressed =
                new Image(GameAtlas[
                    SuffixFromSpritePathOrDefault("/xPressed", "objects/CommunalHelper/cassetteMoveBlock/xPressed")]);
            crossHighlight =
                new Image(GameAtlas[
                    SuffixFromSpritePathOrDefault("/xHighlight",
                        "objects/CommunalHelper/cassetteMoveBlock/xHighlight")]);
            crossHighlightPressed =
                new Image(GameAtlas[
                    SuffixFromSpritePathOrDefault("/xHighlightPressed",
                        "objects/CommunalHelper/cassetteMoveBlock/xHighlightPressed")]);

            base.Awake(scene);
            AddCenterSymbol(arrow, arrowPressed);
            AddCenterSymbol(cross, crossPressed);
            AddCenterSymbol(arrowHighlight, arrowHighlightPressed);
            AddCenterSymbol(crossHighlight, crossHighlightPressed);
        }


        public override void OnStaticMoverTrigger(SourceStaticMoverState sm)
        {
            triggered = true;
        }
    }
}
