// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Platforms.FloatySpaceBlocks;
using Celeste.Game.Gameplay.Mathematics.Platforms;
using Celeste.Graphics.Animation;
using Celeste.Core.Scheduling;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.FloatySpaceBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    [CustomEntity("CommunalHelper/DreamFloatySpaceBlock")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.DreamFloatySpaceBlock")]
    public class DreamFloatySpaceBlock : ConnectedDreamBlock
    {
        private static readonly OriginalFloatySpaceArithmetic floatyArithmetic = new();
        private Vector2 dashDirection;
        private float dashEase;
        private float sineWave;
        private float sinkTimer;
        private float yLerp;

        public static new DreamFloatySpaceBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset, level.SolidWorldFactory);

        public DreamFloatySpaceBlock(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds) : base(engine, data, offset, worlds)
        {
            IncludeJumpThrus = true;
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            TryToInitPosition();
        }

        private void TryToInitPosition()
        {
            if (MasterOfGroup)
            {
                foreach (ConnectedDreamBlock block in Group)
                {
                    if (block is not DreamFloatySpaceBlock floatyBlock || !floatyBlock.awake)
                        return;
                }

                MoveToTarget();
                return;
            }

            (master as DreamFloatySpaceBlock)?.TryToInitPosition();
        }

        private void MoveToTarget()
        {
            float sine = floatyArithmetic.GetSine(sineWave);
            Vector2 displacement =
                ScalarMath.YoYo(((ISourceEasingEnvironment)Engine).EasingArithmetic.QuadIn(dashEase)) *
                dashDirection * 8f;
            FloatyPlatformGroupMotion.Move(ref Moves, sine, displacement, new FloatyTarget(this));
        }

        private readonly struct FloatyTarget : IFloatyPlatformTarget
        {
            private readonly DreamFloatySpaceBlock owner;
            public FloatyTarget(DreamFloatySpaceBlock owner) => this.owner = owner;

            public float GetTargetY(float origin, float sine, float dashY)
                => floatyArithmetic.GetTargetY(origin,
                    ((ISourceEasingEnvironment)owner.Engine).EasingArithmetic.SineInOut(owner.yLerp), sine, dashY);
        }

        protected override DashCollisionResult OnDash(RoomPlayerSession player, Vector2 dir)
        {
            if (!PlayerHasDreamDash)
            {
                if (MasterOfGroup && dashEase <= 0.2f)
                {
                    dashEase = 1f;
                    dashDirection = dir;
                }

                return DashCollisionResult.NormalOverride;
            }

            return DashCollisionResult.NormalCollision;
        }

        protected override DashCollisionResult OnDashJumpThru(RoomPlayerSession player, Vector2 dir)
        {
            if (MasterOfGroup && dashEase <= 0.2f)
            {
                dashEase = 1f;
                dashDirection = dir;
            }

            return DashCollisionResult.NormalOverride;
        }

        public override void OnShake(Vector2 amount)
        {
            if (!MasterOfGroup)
                return;
            base.OnShake(amount);
            SourceShake += amount;
            foreach (var jumpThru in JumpThrus)
            {
                foreach (SourceComponentState component in jumpThru.OwnedComponents)
                {
                    if (component is SpriteImage image)
                        image.Position += amount;
                }
            }
        }

        public override void OnStaticMoverTrigger(SourceStaticMoverState mover)
            => FloatySpringReaction.Apply(mover, ref sinkTimer, ref dashEase, ref dashDirection);

        public override void Update()
        {
            base.Update();
            if (MasterOfGroup && ShatterCheck())
            {
                bool hasPlayerRider = false;
                foreach (ConnectedDreamBlock block in Group)
                {
                    if (block.HasPlayerRider())
                    {
                        hasPlayerRider = true;
                        break;
                    }
                }

                if (!hasPlayerRider)
                {
                    foreach (var jumpThru in JumpThrus)
                    {
                        if (jumpThru.HasPlayerRider())
                        {
                            hasPlayerRider = true;
                            break;
                        }
                    }
                }

                if (hasPlayerRider)
                    sinkTimer = 0.3f;
                else if (sinkTimer > 0f)
                    sinkTimer -= Engine.DeltaTime;
                yLerp = sinkTimer > 0f
                    ? ScalarMath.Approach(yLerp, 1f, 1f * Engine.DeltaTime,
                        ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic)
                    : ScalarMath.Approach(yLerp, 0f, 1f * Engine.DeltaTime,
                        ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic);
                sineWave += Engine.DeltaTime;
                dashEase = ScalarMath.Approach(dashEase, 0f, Engine.DeltaTime * 1.5f,
                    ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic);
                MoveToTarget();
            }

            LiftSpeed = Vector2.Zero;
        }
    }
}
