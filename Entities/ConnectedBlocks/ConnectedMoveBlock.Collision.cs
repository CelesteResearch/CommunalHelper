// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Numerics;
using Celeste.Everest.Content.Extensions.Entities.Registry;
using Celeste.Everest.Reflection;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Mods.CommunalHelper.Physics;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedMoveBlock
    {
        // Resolve once at map construction, never in the movement pixel loop.
        private Type[] ResolveIgnoredTypes(string names)
        {
            string[] entries = names.Split(',').Select(s => s.Trim()).Where(s => s.Length != 0).ToArray();
            if (entries.Length == 0) return [];
            var registry = ((IEverestEntityRegistryEnvironment)SourceWorld.EngineContext).EntityRegistry;
            var found = entries.SelectMany(registry.GetKnownTypesFromSid).ToList();
            var aggregate =
                ((ISourceEverestReflectionEnvironment)SourceWorld.EngineContext).Reflection.GetFakeEntryAssembly();
            foreach (string name in entries)
            {
                Type type = aggregate.GetType(name, false);
                if (type != null) found.Add(type);
            }

            return found.Distinct().ToArray();
        }

        private bool CollideSolidsExcluding(Vector2 at)
        {
            return SolidCollisionExclusions.Check(this, ignores, at);
        }

        private bool MoveHCollideSolidsExcluding(float amount)
        {
            return SolidCollisionExclusions.MoveHorizontal(this, SourceWorld.EngineContext, ignores, amount);
        }

        private bool MoveVCollideSolidsExcluding(float amount)
        {
            return SolidCollisionExclusions.MoveVertical(this, SourceWorld.EngineContext, ignores, amount);
        }

        private bool CollideJumpThrusOutsideExcluding(Vector2 at)
        {
            return SolidCollisionExclusions.CheckJumpOutside(this, ignores, at);
        }
    }
}
