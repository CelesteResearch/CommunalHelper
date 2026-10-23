using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Walls.Breakable;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.DreamFallingBlock")]
    [CustomEntity("CommunalHelper/DreamFallingBlock")]
    public class DreamFallingBlock : CustomDreamBlock
    {
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;
        private IOriginalGameplayProcess Process => (IOriginalGameplayProcess)Engine;
        private SceneEntityRegistry Registry => ((ISourceScene)Scene).SourceScene.Entities;
        private readonly bool forceShake;

        public static DreamFallingBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) =>
            new(level.EngineContext, data, offset, level.SolidWorldFactory);

        public bool Triggered;
        public float FallDelay;

        private readonly bool noCollide;

        public bool HasStartedFalling { get; private set; }
        protected bool hasLanded;

        protected bool removeWhenOutOfLevel = false;
        protected virtual bool Held => CollideCheck("Celeste.Platform", Position + new Vector2(0f, 1f));

        private readonly bool legacyLandingBehavior;

        public DreamFallingBlock(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : base(engine, data, offset, worlds)
        {
            noCollide = data.Bool("noCollide", false);
            forceShake = data.Bool("forceShake", false);
            legacyLandingBehavior = data.Bool("legacyLandingBehavior", true);

            Add(new SimulationCoroutine(engine, Sequence()));
        }

        public override void OnStaticMoverTrigger(SourceStaticMoverState sm)
        {
            Triggered = true;
        }

        private bool PlayerWaitCheck()
            => Triggered
               || forceShake
               || HasPlayerRider()
               || CollideCheck(typeof(Celeste.Simulation.RoomPlayerSession).FullName, Position - Vector2.UnitX)
               || CollideCheck(typeof(Celeste.Simulation.RoomPlayerSession).FullName, Position + Vector2.UnitX);

        private IEnumerator Sequence()
        {
            while (!Triggered && !HasPlayerRider())
                yield return null;

            while (FallDelay > 0f)
            {
                FallDelay -= Engine.DeltaTime;
                yield return null;
            }

            HasStartedFalling = true;
            while (true)
            {
                ShakeSfx();
                StartShaking();
                ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Medium,
                    RumbleLength.Medium);
                yield return 0.2f;

                float timer = 0.4f;
                while (timer > 0f && PlayerWaitCheck())
                {
                    yield return null;
                    timer -= Engine.DeltaTime;
                }

                StopShaking();
                for (int i = 2; i < Width; i += 4)
                {
                    if (SourceSceneCollision.CheckPoint(Registry, "Celeste.Solid", TopLeft + new Vector2(i, -2f)))
                    {
                        Level.Particles.Emit(Process.ParticleTypes.FallingBlock.P_FallDustA, 2, new Vector2(X + i, Y),
                            Vector2.One * 4f, (float)Math.PI / 2f);
                    }

                    Level.Particles.Emit(Process.ParticleTypes.FallingBlock.P_FallDustB, 2, new Vector2(X + i, Y),
                        Vector2.One * 4f);
                }

                FallSFX();

                float speed = 0f;
                float maxSpeed = 160f;
                hasLanded = false;
                while (true)
                {
                    OriginalLevelSceneRoot level = Level;
                    speed = ScalarMath.Approach(speed, maxSpeed, 500f * Engine.DeltaTime);
                    MoveV(speed * Engine.DeltaTime);
                    if (ShouldStopFalling())
                        break;

                    if ((removeWhenOutOfLevel && Top > level.Bounds.Bottom + 16) || (Top > level.Bounds.Bottom - 1 &&
                            CollideCheck("Celeste.Solid", Position + new Vector2(0f, 1f))))
                    {
                        Collidable = Visible = false;
                        yield return 0.2f;

                        if (((OriginalChapterMapData)level.Session.MapData).CanTransitionTo(level,
                                new Vector2(Center.X, Bottom + 12f)))
                        {
                            yield return 0.2f;

                            Level.Shake();
                            ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Strong,
                                RumbleLength.Medium);
                        }

                        RemoveSelf();
                        DestroyStaticMovers();
                        yield break;
                    }

                    yield return null;
                }

                ImpactSfx();
                ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Strong,
                    RumbleLength.Medium);
                Level.DirectionalShake(Vector2.UnitY, 0.3f);
                StartShaking();
                LandParticles();

                yield return 0.2f;

                StopShaking();
                if (CollideCheck("Celeste.SolidTiles", Position + new Vector2(0f, 1f)))
                {
                    break;
                }

                while (Held)
                {
                    yield return 0.1f;
                }
            }

            Safe = true;
        }

        // Essentially just a copied/stripped version of MoveVExactCollideSolids
        public override void MoveVExact(int move)
        {
            float y = Y;
            int dir = Math.Sign(move);
            int actualMove = 0;
            SourcePlatformEntityState platform = null;
            while (move != 0)
            {
                if (!noCollide)
                {
                    foreach (object entity in Registry.GetTrackedEntities("Celeste.DashBlock"))
                    {
                        if (Celeste.Physics.Collision.SourceEntityCollision.CheckAt(this,
                                (Celeste.Physics.Collision.ISourceEntityCollider)entity,
                                Position + (Vector2.UnitY * dir)))
                        {
                            ((Celeste.Game.Gameplay.Walls.Breakable.OriginalDashBlockState)entity).Break(Center,
                                Vector2.UnitY * dir, true, true);
                            Level.Shake(0.2f);
                            ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Medium,
                                RumbleLength.Medium);
                        }
                    }
                }

                platform = noCollide
                    ? CollideFirst<SourcePlatformEntityState>("Celeste.DreamBlock", Position + (Vector2.UnitY * dir))
                    : CollideFirst<SourcePlatformEntityState>("Celeste.Solid", Position + (Vector2.UnitY * dir));
                if (platform is not null)
                    break;

                if (!noCollide && move > 0)
                {
                    platform =
                        SourceTrackedEntityCollision.FirstOutsideAt<SourcePlatformEntityState, DreamFallingBlock>(
                            Registry, "Celeste.JumpThru", this, Position + (Vector2.UnitY * dir));
                    if (platform is not null)
                        break;
                }

                actualMove += dir;
                move -= dir;
                Y += dir;
            }

            Y = y;
            base.MoveVExact(actualMove);
            hasLanded = platform is not null;
        }

        public override void Render(ISourceRenderContext context)
        {
            Position += Shake;
            base.Render(context);
            Position -= Shake;
        }

        private void LandParticles()
        {
            for (int i = 2; i <= Width; i += 4)
            {
                if (SourceSceneCollision.CheckPoint(Registry, "Celeste.Solid", BottomLeft + new Vector2(i, 3f)))
                {
                    Level.ParticlesFG.Emit(Process.ParticleTypes.FallingBlock.P_FallDustA, 1,
                        new Vector2(X + i, Bottom), Vector2.One * 4f, -(float)Math.PI / 2f);
                    float direction = (!(i < Width / 2f)) ? 0f : ((float)Math.PI);
                    Level.ParticlesFG.Emit(Process.ParticleTypes.FallingBlock.P_LandDust, 1, new Vector2(X + i, Bottom),
                        Vector2.One * 4f, direction);
                }
            }
        }

        protected virtual bool ShouldStopFalling()
        {
            return !legacyLandingBehavior && hasLanded;
        }

        private void ShakeSfx()
        {
            ((Celeste.Core.Audio.ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                "event:/game/general/fallblock_shake", Center);
        }

        protected virtual void FallSFX()
        {
        }

        protected virtual void ImpactSfx()
        {
            ((Celeste.Core.Audio.ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                "event:/game/general/fallblock_impact", BottomCenter);
        }
    }
}
