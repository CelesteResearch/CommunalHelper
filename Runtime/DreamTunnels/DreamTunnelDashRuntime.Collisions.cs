using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Game.Entities.Platforms.Dream;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Mechanisms.DashSwitches;
using Celeste.Game.Gameplay.Platforms.FloatySpaceBlocks;
using Celeste.Game.Gameplay.Walls.Breakable;
using Celeste.Mods.CommunalHelper.Components.DreamTunnels;
using Celeste.Mods.CommunalHelper.Entities.DreamTunnels;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Effects.Trails.Lifecycle;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamTunnels
{
    public sealed partial class DreamTunnelDashRuntime
    {
        private static Vector2 Sign(Vector2 value) => new Vector2(Math.Sign(value.X), Math.Sign(value.Y));

        private static SourceSolidEntityState SolidExceptDreamAt(RoomPlayerSession player, Vector2 at)
        {
            var registry = ((ISourceScene)player.Scene).SourceScene.Entities;
            var excluded = registry.GetTrackedEntities("Celeste.DreamBlock");
            foreach (SourceSolidEntityState solid in registry.GetTrackedEntities("Celeste.Solid"))
                if (!excluded.Contains(solid) && SourceEntityCollision.CheckAt(player, solid, at))
                    return solid;
            return null;
        }

        private bool DreamTunneledIntoDeath(RoomPlayerSession player)
        {
            if (!player.CollideCheck("Celeste.DreamBlock")) return false;
            for (int x = 1; x <= 5; x++)
            for (int signX = -1; signX <= 1; signX += 2)
            for (int y = 1; y <= 5; y++)
            for (int signY = -1; signY <= 1; signY += 2)
            {
                Vector2 offset = new Vector2(x * signX, y * signY);
                if (!player.CollideCheck("Celeste.DreamBlock", player.Position + offset))
                {
                    player.Position += offset;
                    return false;
                }
            }

            return true;
        }

        private bool DreamDashDie(RoomPlayerSession player, Vector2 previous, bool evenIfInvincible = false)
        {
            if (!evenIfInvincible && player.Settings.Invincible)
            {
                player.Position = previous;
                player.Data.Motion.Normal.Speed *= -1f;
                player.Play("event:/game/general/assist_dreamblockbounce");
                return false;
            }

            player.Die(Vector2.Zero, evenIfInvincible, true);
            return true;
        }

        private bool DreamTunnelCheck(RoomPlayerSession player, Vector2 dir)
        {
            Vector2 dashDirection = player.Data.Direction;
            if (IsInverted(player))
            {
                dir.Y *= -1f;
                dashDirection.Y *= -1f;
            }

            if (!attacking || !player.Data.DashAttacking(player.Machine.State) ||
                dir.X != Math.Sign(dashDirection.X) && dir.Y != Math.Sign(dashDirection.Y)) return false;
            var bounds = ((OriginalLevelSceneRoot)player.Scene).Bounds;
            if (player.Left + dir.X < bounds.Left || player.Right + dir.X > bounds.Right ||
                player.Top + dir.Y < bounds.Top || player.Bottom + dir.Y > bounds.Bottom) return false;
            if (BlockerConfiguration(player, player.Position + dir).Tunnel) return false;
            SourceSolidEntityState solid = null;
            var block = player.CollideFirst<OriginalDreamBlockState>("Celeste.DreamBlock", player.Position + dir);
            if (block != null)
            {
                Vector2 side = new Vector2(Math.Abs(dir.Y), Math.Abs(dir.X));
                bool enteredDreamBlock = true;
                bool checkNegative = dir.X != 0f
                    ? player.Data.Motion.Normal.Speed.Y <= 0f
                    : player.Data.Motion.Normal.Speed.X <= 0f;
                bool checkPositive = dir.X != 0f
                    ? player.Data.Motion.Normal.Speed.Y >= 0f
                    : player.Data.Motion.Normal.Speed.X >= 0f;
                if (checkNegative)
                    for (int i = -1; i >= -4; i--)
                    {
                        Vector2 at = player.Position + dir + side * i;
                        if (!player.CollideCheck("Celeste.DreamBlock", at) &&
                            (solid = SolidExceptDreamAt(player, at)) != null)
                        {
                            player.Position += side * i;
                            enteredDreamBlock = false;
                            goto CheckDreamBlock;
                        }
                    }

                if (checkPositive)
                    for (int i = 1; i <= 4; i++)
                    {
                        Vector2 at = player.Position + dir + side * i;
                        if (!player.CollideCheck("Celeste.DreamBlock", at) &&
                            (solid = SolidExceptDreamAt(player, at)) != null)
                        {
                            player.Position += side * i;
                            enteredDreamBlock = false;
                            goto CheckDreamBlock;
                        }
                    }

                CheckDreamBlock:
                if (enteredDreamBlock)
                {
                    if (block.PlayerHasDreamDash) player.Die(-dir);
                    attacking = false;
                    overrideDreamDashCheck = true;
                    return false;
                }
            }

            solid ??= SolidExceptDreamAt(player, player.Position + dir);
            if (solid != null && (!Settings.DreamTunnelIgnoreCollidables || solid.OnDashCollide == null ||
                                  solid is FloatySpaceBlockState ||
                                  solid is OriginalDashBlockState dashBlock && !dashBlock.CanDash))
            {
                player.Machine.State = GetPlayerState(player).Index;
                foreach (var interaction in solid.Components.GetAll<DreamTunnelInteraction>().ToList())
                    interaction.OnPlayerEnter(player);
                GetPlayerState(player).Solid = solid;
                player.Data.Motion.DashAttackTimer = 0f;
                player.Data.Motion.GliderBoostTimer = 0f;
                return true;
            }

            if (solid is OriginalDashSwitchState) attacking = false;
            return false;
        }

        private (bool Tunnel, bool Dream) BlockerConfiguration(RoomPlayerSession player, Vector2 at)
        {
            bool tunnel = false;
            bool dream = false;
            foreach (DreamTunnelBlocker blocker in ((ISourceScene)player.Scene).SourceScene.Entities
                     .GetTrackedEntities(typeof(DreamTunnelBlocker)))
            {
                if (!SourceEntityCollision.CheckAt(player, blocker, at)) continue;
                if (blocker.BlockDreamTunnelDashes) tunnel = true;
                if (blocker.BlockDreamDashes) dream = true;
            }

            return (tunnel, dream);
        }

        private void CreateDreamTrail(RoomPlayerSession player)
        {
            Vector2 scale = new Vector2(Math.Abs(player.Sprite.Scale.X) * player.Data.Facing, player.Sprite.Scale.Y);
            if (IsInverted(player)) scale.Y *= -1f;
            TrailManagerState.Add(Engine, (ISourceRenderEnvironment)Engine, player, scale,
                trailColors[trailColorIndex]);
            trailColorIndex++;
            trailColorIndex %= 5;
        }

        private void PlayGlobal(string path)
        {
            var process = (IOriginalGameplayProcess)Engine;
            process.AudioSession.Play(path);
        }
    }
}
