using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Everest.Runtime.Events.Player;
using Celeste.Game.Execution.Levels;
using Celeste.Physics.Collision;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Collision.Responses;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Gameplay.Player.State;
using Celeste.Simulation.Presentation.Player.Death;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamTunnels
{
    public sealed partial class DreamTunnelDashRuntime : IDisposable
    {
        [ThreadStatic] private static RoomPlayerSession updatingPlayer;
        private static readonly ConditionalWeakTable<PlayerState, RoomPlayerSession> dataOwners = new();
        private readonly List<IDisposable> hooks = new();
        private bool loaded;

        public void Load()
        {
            if (loaded) throw new InvalidOperationException("Dream tunnel runtime is already loaded.");
            loaded = true;
            try
            {
                LoadGravityImports();
                EverestPlayerSourceEvents.OnRegisterStates += RegisterPlayerState;
                EverestPlayerSourceEvents.OnDie += RestoreConfigurationOnDeath;
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetConstructors()[0],
                    (PlayerConstructor)AfterConstructor));
                hooks.Add(new Hook(typeof(DashSequence).GetMethod(nameof(DashSequence.Begin)),
                    (Action<Action<DashSequence>, DashSequence>)DashBegin));
                hooks.Add(new Hook(PlayerMethod("CreateTrail"),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)CreateTrail));
                hooks.Add(new Hook(
                    typeof(PlayerCollisionResponse).GetMethod(nameof(PlayerCollisionResponse.Horizontal)),
                    (CollisionHook)CollideH));
                hooks.Add(new Hook(typeof(PlayerCollisionResponse).GetMethod(nameof(PlayerCollisionResponse.Vertical)),
                    (CollisionHook)CollideV));
                hooks.Add(new Hook(PlayerMethod("DreamDashCheck"),
                    (Func<Func<RoomPlayerSession, Vector2, bool>, RoomPlayerSession, Vector2, bool>)DreamDashCheck));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.UpdateSourceBody)),
                    (Action<Action<RoomPlayerSession, float, float>, RoomPlayerSession, float, float>)UpdatePlayer));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Die)), (DieHook)Die));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.OnBoundsH)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)OnBounds));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.OnBoundsV)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)OnBounds));
                hooks.Add(new Hook(PlayerMethod("DreamDashBegin"),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)DreamDashBegin));
                InstallStateChecks();
                InstallLevelLifecycle();
            }
            catch (Exception failure)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(failure, cleanup);
                }

                throw;
            }
        }

        private delegate void OriginalPlayerConstructor(RoomPlayerSession player, Vector2 position,
            Celeste.Simulation.Gameplay.Player.Configuration.IPlayerAssistSettings settings,
            Celeste.Input.Gameplay.ISourceGameInput input, ISourceEngineContext engine,
            Celeste.Simulation.Gameplay.Platforms.Collision.PlatformCollisionWorld platforms,
            IReadOnlyDictionary<string, string> chaseSoundMapping,
            Celeste.Simulation.Gameplay.Player.Presentation.Sprites.PlayerSpriteMode mode);

        private delegate void PlayerConstructor(OriginalPlayerConstructor original, RoomPlayerSession player,
            Vector2 position, Celeste.Simulation.Gameplay.Player.Configuration.IPlayerAssistSettings settings,
            Celeste.Input.Gameplay.ISourceGameInput input, ISourceEngineContext engine,
            Celeste.Simulation.Gameplay.Platforms.Collision.PlatformCollisionWorld platforms,
            IReadOnlyDictionary<string, string> chaseSoundMapping,
            Celeste.Simulation.Gameplay.Player.Presentation.Sprites.PlayerSpriteMode mode);

        private delegate void CollisionHook(
            Action<PlayerCollisionResponse, PixelCollision, SourcePlatformEntityState> original,
            PlayerCollisionResponse response, PixelCollision collision, SourcePlatformEntityState hit);

        private delegate PlayerDeathPresentation DieHook(
            Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation> original,
            RoomPlayerSession player, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats);

        private static MethodInfo PlayerMethod(string name)
            => typeof(RoomPlayerSession).GetMethod(name,
                   BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
               ?? throw new MissingMethodException(typeof(RoomPlayerSession).FullName, name);

        private void AfterConstructor(OriginalPlayerConstructor original, RoomPlayerSession player, Vector2 position,
            Celeste.Simulation.Gameplay.Player.Configuration.IPlayerAssistSettings settings,
            Celeste.Input.Gameplay.ISourceGameInput input, ISourceEngineContext engine,
            Celeste.Simulation.Gameplay.Platforms.Collision.PlatformCollisionWorld platforms,
            IReadOnlyDictionary<string, string> chaseSoundMapping,
            Celeste.Simulation.Gameplay.Player.Presentation.Sprites.PlayerSpriteMode mode)
        {
            original(player, position, settings, input, engine, platforms, chaseSoundMapping, mode);
            if (!ReferenceEquals(engine, Engine)) return;
            canStartAttack = false;
            attacking = false;
        }

        private void DashBegin(Action<DashSequence> original, DashSequence sequence)
        {
            original(sequence);
            if (!dataOwners.TryGetValue(sequence.Data, out var player) ||
                !ReferenceEquals(player.EngineContext, Engine)) return;
            UseCharge();
            if (!Configuration.AllowDashCancels) StartAttacking(player);
        }

        private void StartAttacking(RoomPlayerSession player, Vector2? coroutineDirection = null)
        {
            if (canStartAttack)
            {
                attacking = true;
                attackTimer = player.Data.Motion.DashAttackTimer;
                Vector2 direction = coroutineDirection ?? player.Input.GetAimVector(player.Data.Facing);
                Vector2 sign = Sign(direction);
                if (SolidExceptDreamAt(player, player.Position) == null &&
                    SolidExceptDreamAt(player, player.Position + sign) != null)
                {
                    if (coroutineDirection == null) player.Data.Motion.Normal.Speed = player.Data.Direction = direction;
                    var state = GetPlayerState(player);
                    player.MoveHExact((int)sign.X, state.OnCollideH);
                    player.MoveVExact((int)sign.Y, state.OnCollideV);
                }
            }

            canStartAttack = false;
        }

        private void CreateTrail(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, Engine) && attacking) CreateDreamTrail(player);
            else original(player);
        }

        private void CollideH(Action<PlayerCollisionResponse, PixelCollision, SourcePlatformEntityState> original,
            PlayerCollisionResponse response, PixelCollision collision, SourcePlatformEntityState hit)
        {
            var player = response.Player;
            if (!ReferenceEquals(player.EngineContext, Engine) ||
                !DreamTunnelCheck(player, Vector2.UnitX * Math.Sign(player.Data.Motion.Normal.Speed.X)))
                original(response, collision, hit);
        }

        private void CollideV(Action<PlayerCollisionResponse, PixelCollision, SourcePlatformEntityState> original,
            PlayerCollisionResponse response, PixelCollision collision, SourcePlatformEntityState hit)
        {
            var player = response.Player;
            if (!ReferenceEquals(player.EngineContext, Engine) ||
                !DreamTunnelCheck(player, Vector2.UnitY * Math.Sign(player.Data.Motion.Normal.Speed.Y)))
                original(response, collision, hit);
        }

        private bool DreamDashCheck(Func<RoomPlayerSession, Vector2, bool> original, RoomPlayerSession player,
            Vector2 direction)
        {
            if (!ReferenceEquals(player.EngineContext, Engine)) return original(player, direction);
            if (overrideDreamDashCheck) return overrideDreamDashCheck = false;
            if (BlockerConfiguration(player, player.Position + direction).Dream) return false;
            return original(player, direction);
        }

        private void UpdatePlayer(Action<RoomPlayerSession, float, float> original, RoomPlayerSession player, float dt,
            float rawDt)
        {
            if (!ReferenceEquals(player.EngineContext, Engine))
            {
                original(player, dt, rawDt);
                return;
            }

            var previous = updatingPlayer;
            updatingPlayer = player;
            try
            {
                original(player, dt, rawDt);
            }
            finally
            {
                updatingPlayer = previous;
            }

            float actualTimer = player.Data.Motion.DashAttackTimer;
            if (actualTimer < attackTimer) attackTimer = actualTimer;
            else if (attackTimer > 0f) attackTimer -= Engine.DeltaTime;
            if (attackTimer <= 0f) attacking = false;
            if (ChargeCount > 0 && ((Celeste.Scene.Tracking.ISourceScene)player.Scene).SourceScene.Clock
                .OnInterval(.1f / ChargeCount, Engine)) CreateDreamTrail(player);
        }

        private PlayerDeathPresentation Die(
            Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation> original,
            RoomPlayerSession player, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
        {
            if (ReferenceEquals(player.EngineContext, Engine)) player.DreamLoopSound?.Stop();
            return original(player, direction, evenIfInvincible, registerDeathInStats);
        }

        private void OnBounds(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, Engine) && player.Machine.State == GetPlayerState(player).Index)
                DreamDashDie(player, player.Position);
            else original(player);
        }

        private void DreamDashBegin(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            original(player);
            if (ReferenceEquals(player.EngineContext, Engine)) NotifyDreamDash(player);
        }

        private void RestoreConfigurationOnDeath(RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, Engine))
                Triggers.DreamTunnels
                    .AbstractConfigureStateTrigger<Configuration.DreamTunnels.DreamTunnelDashConfiguration,
                        Configuration.DreamTunnels.DreamTunnelDashConfigurationChanges>.RestoreOnDeath(player);
        }

        private static void DisposeLeases(List<IDisposable> leases, List<Exception> failures)
        {
            for (int i = leases.Count - 1; i >= 0; i--)
            {
                try
                {
                    leases[i].Dispose();
                }
                catch (Exception failure)
                {
                    failures.Add(failure);
                }
            }

            leases.Clear();
        }

        public void Dispose()
        {
            if (!loaded) return;
            loaded = false;
            EverestPlayerSourceEvents.OnRegisterStates -= RegisterPlayerState;
            EverestPlayerSourceEvents.OnDie -= RestoreConfigurationOnDeath;
            var failures = new List<Exception>();
            DisposeLeases(hooks, failures);
            try
            {
                ReleaseStateChecks();
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }

            var imports = gravityImports;
            gravityImports = null;
            try
            {
                imports?.Dispose();
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }

            if (failures.Count != 0) throw new AggregateException(failures);
        }
    }
}
