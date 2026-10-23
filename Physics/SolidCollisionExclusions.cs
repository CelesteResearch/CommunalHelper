// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.CommunalHelper.Physics
{
    internal static class SolidCollisionExclusions
    {
        private static bool Ignored(Type[] types, Type actual)
        {
            for (int i = 0; i < types.Length; i++)
                if (types[i] == actual)
                    return true;
            return false;
        }

        internal static bool Check(SourcePlatformEntityState entity, Type[] ignores, Vector2 at)
        {
            if (ignores.Length == 0) return entity.CollideCheck("Celeste.Solid", at);
            return First(entity, ignores, "Celeste.Solid", at, false) != null;
        }

        private static SceneEntityState First(SourcePlatformEntityState entity, Type[] ignores, string key, Vector2 at,
            bool outside)
        {
            var list = ((ISourceScene)entity.Scene).SourceScene.Entities.GetTrackedEntities(key);
            for (int i = 0; i < list.Count; i++)
            {
                var target = (SceneEntityState)list[i];
                if (!Ignored(ignores, target.GetType()) && (!outside || !SourceEntityCollision.Check(entity, target)) &&
                    SourceEntityCollision.CheckAt(entity, target, at)) return target;
            }

            return null;
        }

        internal static bool CheckJumpOutside(SourcePlatformEntityState entity, Type[] ignores, Vector2 at)
            => First(entity, ignores, "Celeste.JumpThru", at, true) != null;

        internal static bool CheckPoint(object scene, Type[] ignores, Vector2 at)
        {
            var list = ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities("Celeste.Solid");
            for (int i = 0; i < list.Count; i++)
            {
                var target = (SceneEntityState)list[i];
                if (!Ignored(ignores, target.GetType()) && target.Collidable && target.Collider != null &&
                    target.Collider.Collide(at)) return true;
            }

            return false;
        }

        internal static bool MoveHorizontal(SourcePlatformEntityState entity, ISourceEngineContext engine,
            Type[] ignores,
            float amount)
        {
            if (ignores.Length == 0) return entity.MoveHCollideSolids(amount, false);
            var lift = entity.LiftSpeed;
            lift.X = engine.DeltaTime == 0f ? 0f : amount / engine.DeltaTime;
            entity.LiftSpeed = lift;
            var counter = entity.MovementCounter;
            counter.X += amount;
            entity.MovementCounter = counter;
            int move = (int)Math.Round(entity.MovementCounter.X);
            if (move == 0) return false;
            counter = entity.MovementCounter;
            counter.X -= move;
            entity.MovementCounter = counter;
            float x = entity.X;
            int sign = Math.Sign(move), moved = 0;
            SceneEntityState collision = null;
            while (move != 0)
            {
                collision = First(entity, ignores, "Celeste.Solid", entity.Position + Vector2.UnitX * sign, false);
                if (collision != null) break;
                moved += sign;
                move -= sign;
                entity.X += sign;
            }

            entity.X = x;
            entity.MoveHExact(moved);
            return collision != null;
        }

        internal static bool MoveVertical(SourcePlatformEntityState entity, ISourceEngineContext engine, Type[] ignores,
            float amount)
        {
            if (ignores.Length == 0) return entity.MoveVCollideSolids(amount, false);
            var lift = entity.LiftSpeed;
            lift.Y = engine.DeltaTime == 0f ? 0f : amount / engine.DeltaTime;
            entity.LiftSpeed = lift;
            var counter = entity.MovementCounter;
            counter.Y += amount;
            entity.MovementCounter = counter;
            int move = (int)Math.Round(entity.MovementCounter.Y);
            if (move == 0) return false;
            counter = entity.MovementCounter;
            counter.Y -= move;
            entity.MovementCounter = counter;
            float y = entity.Y;
            int sign = Math.Sign(move), moved = 0;
            SceneEntityState collision = null;
            while (move != 0)
            {
                collision = First(entity, ignores, "Celeste.Solid", entity.Position + Vector2.UnitY * sign, false);
                if (collision != null) break;
                if (move > 0)
                {
                    collision = First(entity, ignores, "Celeste.JumpThru", entity.Position + Vector2.UnitY * sign,
                        true);
                    if (collision != null) break;
                }

                moved += sign;
                move -= sign;
                entity.Y += sign;
            }

            entity.Y = y;
            entity.MoveVExact(moved);
            return collision != null;
        }
    }
}
