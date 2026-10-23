using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Mods.CommunalHelper.Components.DreamTunnels;
using Celeste.Mods.CommunalHelper.Configuration.DreamTunnels;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Dash;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamTunnels
{
    public sealed partial class DreamTunnelDashRuntime
    {
        private readonly ConditionalWeakTable<RoomPlayerSession, DreamTunnelPlayerState> players = new();
        private ISourceVectorArithmetic Vectors => ((ISourceVectorEnvironment)Engine).VectorArithmetic;

        private DreamTunnelPlayerState GetPlayerState(RoomPlayerSession player)
            => players.GetValue(player, _ => new DreamTunnelPlayerState());

        private void RegisterPlayerState(RoomPlayerSession player)
        {
            if (!ReferenceEquals(player.EngineContext, Engine)) return;
            var state = GetPlayerState(player);
            dataOwners.Add(player.Data, player);
            state.OnCollideH = collision => player.Motor.Collisions.Horizontal(collision,
                player.World.CollisionSurface(collision));
            state.OnCollideV = collision => player.Motor.Collisions.Vertical(collision,
                player.World.CollisionSurface(collision));
            state.Index = ((ISourceNamedStateMachine)player.Machine).AddState("DreamTunnelDash",
                () => UpdateState(player), null, () => BeginState(player), () => EndState(player));
        }

        private void BeginState(RoomPlayerSession player)
        {
            var state = GetPlayerState(player);
            var config = Configuration;
            player.Data.StartedDashing = false;
            var sound = player.EnsureDreamLoopSound(!config.AllowTransitions);
            Vector2 dir = Sign(player.Data.Direction);
            if (SolidExceptDreamAt(player, player.Position) == null &&
                SolidExceptDreamAt(player, player.Position + dir) != null)
                player.NaiveMove(dir);
            if (player.Data.Direction.Y > 0f) player.Data.Motion.Ducking = false;
            float speed = Vectors.Length(player.Data.Motion.Normal.Speed);
            Vector2 entryDirection = config.UseEntryDirection && speed > 240f
                ? Vectors.SafeNormalize(player.Data.Motion.Normal.Speed)
                : player.Data.Direction;
            player.Data.Motion.Normal.Speed = entryDirection * EntrySpeed(config, speed);
            player.TreatNaive = true;
            player.Depth = -12000;
            state.CanEndTimer = .1f;
            player.Data.Stamina = 110f;
            player.Data.DreamJump = false;
            player.Play("event:/char/madeline/dreamblock_enter");
            player.Loop(sound, FeatherMode
                ? "event:/CommunalHelperEvents/game/connectedDreamBlock/dreamblock_fly_travel"
                : "event:/char/madeline/dreamblock_travel");
            NotifyDreamDash(player);
        }

        private static float EntrySpeed(DreamTunnelDashConfiguration config, float speed)
            => config.SpeedConfiguration switch
            {
                DreamTunnelSpeedConfiguration.Default => 240f,
                DreamTunnelSpeedConfiguration.NeverSlowDown => Math.Max(speed, 240f),
                DreamTunnelSpeedConfiguration.UseCustomSpeed => config.CustomSpeed,
                _ => 0f
            };

        private void EndState(RoomPlayerSession player)
        {
            var state = GetPlayerState(player);
            player.Depth = 0;
            if (!player.Data.DreamJump)
            {
                player.Data.Motion.AutoJump = true;
                player.Data.AutoJumpTimer = 0f;
            }

            if (!player.Inventory.NoRefills) player.RefillDash();
            player.RefillStamina();
            player.TreatNaive = false;
            var solid = state.Solid;
            if (solid != null)
            {
                if (player.Data.Direction.X != 0f)
                {
                    player.Data.Motion.JumpGraceTimer = .1f;
                    player.Data.DreamJump = true;
                }
                else
                    player.Data.Motion.JumpGraceTimer = 0f;

                foreach (var interaction in solid.Components.GetAll<DreamTunnelInteraction>().ToList())
                    interaction.OnPlayerExit(player);
            }

            player.Stop(player.DreamLoopSound);
            player.Play("event:/char/madeline/dreamblock_exit");
            ((ISourceRumbleEnvironment)Engine).RumbleSession.Rumble(1, 0);
        }

        private void Redirect(RoomPlayerSession player)
        {
            var config = Configuration;
            if (config.RedirectConsumesNormalDash ? player.Data.Dashes <= 0 : ChargeCount <= 0) return;
            bool sameDirection = Sign(player.Input.GetAimVector()) == Sign(player.Data.Motion.Normal.Speed);
            if ((!config.AllowRedirect || sameDirection) &&
                (!config.AllowSameDirectionRedirect || !sameDirection)) return;
            if (config.RedirectConsumesNormalDash)
                player.Data.Dashes = Math.Max(0, player.Data.Dashes - 1);
            else
                ChargeCount = Math.Max(0, ChargeCount - 1);
            PlayGlobal("event:/char/madeline/dreamblock_enter");
            if (Engine.Effects.TimeRate > .25f) player.Freeze(.05f);
            if (sameDirection)
            {
                player.Data.Motion.Normal.Speed *= config.SameDirectionSpeedMultiplier;
                player.Data.Direction *= Math.Sign(config.SameDirectionSpeedMultiplier);
            }
            else
            {
                player.Data.Direction = player.Input.GetAimVector();
                player.Data.Motion.Normal.Speed =
                    player.Data.Direction * Vectors.Length(player.Data.Motion.Normal.Speed);
            }

            player.Input.Dash.ConsumeBuffer();
        }

        private bool AttemptBounce(RoomPlayerSession player)
        {
            if (!Configuration.BounceOnCollision) return false;
            Vector2 move = player.Data.Motion.Normal.Speed * Engine.DeltaTime;
            player.NaiveMove(move);
            if (SolidExceptDreamAt(player, player.Position) == null && DreamTunneledIntoDeath(player))
            {
                player.NaiveMove(-move);
                bool horizontal = OutsideAfterMove(player,
                    new Vector2(player.Data.Motion.Normal.Speed.X * Engine.DeltaTime, 0f));
                bool vertical = OutsideAfterMove(player,
                    new Vector2(0f, player.Data.Motion.Normal.Speed.Y * Engine.DeltaTime));
                if (horizontal) player.Data.Motion.Normal.Speed.X *= -1f;
                if (vertical) player.Data.Motion.Normal.Speed.Y *= -1f;
                return true;
            }

            player.NaiveMove(-move);
            return false;
        }

        private bool OutsideAfterMove(RoomPlayerSession player, Vector2 offset)
        {
            player.NaiveMove(offset);
            bool outside = SolidExceptDreamAt(player, player.Position) == null;
            player.NaiveMove(-offset);
            return outside;
        }

        private int UpdateState(RoomPlayerSession player)
        {
            var state = GetPlayerState(player);
            var config = Configuration;
            if (player.Input.Dash.Pressed && player.Input.Aim.Value != Vector2.Zero) Redirect(player);
            if (AttemptBounce(player)) return state.Index;
            if (FeatherMode)
            {
                Vector2 input = Vectors.SafeNormalize(player.Input.Aim.Value);
                if (input != Vector2.Zero)
                {
                    Vector2 direction = Vectors.SafeNormalize(player.Data.Motion.Normal.Speed);
                    if (direction != Vector2.Zero)
                    {
                        if (Vectors.Dot(input, direction) != -.8f)
                            direction = Vectors.RotateTowards(direction, Vector2Geometry.Angle(input),
                                5f * Engine.DeltaTime);
                        direction = CorrectJoystickPrecision(direction);
                        player.Data.Direction = direction;
                        player.Data.Motion.Normal.Speed =
                            direction * EntrySpeed(config, Vectors.Length(player.Data.Motion.Normal.Speed));
                    }
                }
            }

            ((ISourceRumbleEnvironment)Engine).RumbleSession.Rumble(0, 1);
            Vector2 previous = player.Position;
            Vector2 factor = Vector2.One;
            if (IsInverted(player)) factor.Y = -1f;
            player.NaiveMove(player.Data.Motion.Normal.Speed * factor * Engine.DeltaTime);
            if (state.CanEndTimer > 0f) state.CanEndTimer -= Engine.DeltaTime;
            var solid = SolidExceptDreamAt(player, player.Position);
            if (solid == null)
            {
                if (DreamTunneledIntoDeath(player)) DreamDashDie(player, previous);
                else if (state.CanEndTimer <= 0f)
                {
                    player.Freeze(.05f);
                    if (player.Input.Jump.Pressed && player.Data.Direction.X != 0f)
                    {
                        player.Data.DreamJump = true;
                        player.Jump();
                    }
                    else if (player.Data.Direction.Y >= 0f || player.Data.Direction.X != 0f)
                    {
                        if (player.Data.Direction.X > 0f &&
                            player.CollideCheck("Celeste.Solid", player.Position - Vector2.UnitX * 5f))
                            player.MoveHExact(-5);
                        else if (player.Data.Direction.X < 0f &&
                                 player.CollideCheck("Celeste.Solid", player.Position + Vector2.UnitX * 5f))
                            player.MoveHExact(5);
                        bool left = player.World.ClimbCheck(-1);
                        bool right = player.World.ClimbCheck(1);
                        int moveX = player.Data.MoveX;
                        if (player.Input.GrabCheck && (moveX == 1 && right || moveX == -1 && left))
                        {
                            player.Data.Facing = moveX;
                            if (!player.Settings.NoGrabbing) return 1;
                            player.ClimbTrigger(moveX);
                            player.Data.Motion.Normal.Speed.X = 0f;
                        }
                    }

                    return 0;
                }
            }
            else
            {
                state.Solid = solid;
                var scene = ((ISourceScene)player.Scene).SourceScene;
                if (scene.Clock.OnInterval(.1f, Engine)) CreateDreamTrail(player);
                if (scene.Clock.OnInterval(.04f, Engine))
                {
                    var burst = ((ISourcePlayerDreamDashScene)player.Level).AddDreamDashBurst(player.Center);
                    burst.WorldClipCollider = solid.Collider;
                    burst.WorldClipPadding = 2;
                }
            }

            return state.Index;
        }

        private static Vector2 CorrectJoystickPrecision(Vector2 direction)
        {
            if (direction.X != 0f && Math.Abs(direction.X) < .001f)
            {
                direction.X = 0f;
                direction.Y = Math.Sign(direction.Y);
            }
            else if (direction.Y != 0f && Math.Abs(direction.Y) < .001f)
            {
                direction.Y = 0f;
                direction.X = Math.Sign(direction.X);
            }

            return direction;
        }

        private static void NotifyDreamDash(RoomPlayerSession player)
        {
            var components = ((ISourceScene)player.Scene).SourceScene.Components;
            foreach (DreamDashListener component in components.GetTrackedComponents(
                         components.Catalog.Find(typeof(DreamDashListener)).Name))
                component.OnDreamDash?.Invoke(player.Data.Direction);
        }
    }
}
