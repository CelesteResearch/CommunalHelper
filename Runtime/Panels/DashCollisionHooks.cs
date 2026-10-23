// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;

namespace Celeste.Mods.CommunalHelper.Runtime.Panels
{
    internal static class DashCollisionHooks
    {
        internal delegate DashCollisionResult CollisionHook(Func<RoomPlayerSession, Vector2, DashCollisionResult> orig,
            RoomPlayerSession player, Vector2 direction);

        private static readonly ConditionalWeakTable<Func<RoomPlayerSession, Vector2, DashCollisionResult>, Chain>
            chains = new();

        private sealed class Chain
        {
            internal readonly List<CollisionHook> Hooks = new();
            private readonly Func<RoomPlayerSession, Vector2, DashCollisionResult> original;
            private Func<RoomPlayerSession, Vector2, DashCollisionResult> current;
            internal readonly Func<RoomPlayerSession, Vector2, DashCollisionResult> Handler;

            internal Chain(Func<RoomPlayerSession, Vector2, DashCollisionResult> original)
            {
                this.original = original ?? ((_, _) => DashCollisionResult.NormalCollision);
                current = this.original;
                Handler = Invoke;
            }

            private DashCollisionResult Invoke(RoomPlayerSession player, Vector2 dir) => current(player, dir);

            internal void Rebuild()
            {
                var next = original;
                for (int i = 0; i < Hooks.Count; i++)
                {
                    var orig = next;
                    var hook = Hooks[i];
                    next = (player, direction) => hook(orig, player, direction);
                }

                current = next;
            }
        }

        internal static Func<RoomPlayerSession, Vector2, DashCollisionResult> Apply(
            Func<RoomPlayerSession, Vector2, DashCollisionResult> original, CollisionHook hook)
        {
            if (hook == null) throw new ArgumentNullException(nameof(hook));
            if (original == null || !chains.TryGetValue(original, out var chain))
            {
                chain = new Chain(original);
                chains.Add(chain.Handler, chain);
            }

            chain.Hooks.Add(hook);
            chain.Rebuild();
            return chain.Handler;
        }

        internal static void Remove(Func<RoomPlayerSession, Vector2, DashCollisionResult> original, CollisionHook hook)
        {
            if (hook == null) throw new ArgumentNullException(nameof(hook));
            if (original != null && chains.TryGetValue(original, out var chain))
            {
                chain.Hooks.Remove(hook);
                chain.Rebuild();
            }
        }
    }
}
