// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Everest.Gameplay.Platforms.Dream;
using Celeste.Game.Entities.Platforms.Dream;
using Celeste.Mods.CommunalHelper.Entities.DreamBlocks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamBlocks
{
    public sealed class DreamMoveBlockProcess : IDisposable
    {
        private readonly List<IDisposable> hooks = new();

        private DreamMoveBlockProcess()
        {
            try
            {
                AddRoutine(typeof(OriginalDreamBlockState), nameof(OriginalDreamBlockState.Activate));
                AddRoutine(typeof(EverestDreamBlockState), nameof(EverestDreamBlockState.Deactivate));
                AddRoutine(typeof(EverestDreamBlockState), nameof(EverestDreamBlockState.FastActivate));
                AddRoutine(typeof(EverestDreamBlockState), nameof(EverestDreamBlockState.FastDeactivate));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static IDisposable Install() => new DreamMoveBlockProcess();

        private void AddRoutine(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            var iterator = method.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            var owner = iterator.GetField("<>4__this", BindingFlags.Instance | BindingFlags.Public);
            hooks.Add(new ILHook(iterator.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic),
                il => PatchRoutine(il, owner)));
        }

        private static void PatchRoutine(ILContext il, FieldInfo owner)
        {
            var cursor = new ILCursor(il);
            var matches = 0;
            while (cursor.TryGotoNext(MoveType.Before, instruction =>
                       instruction.OpCode == OpCodes.Callvirt && instruction.Operand is MethodReference method &&
                       method.Name == "Emit" && method.DeclaringType.Name == "ParticleSystemPresentation"))
            {
                var emission = cursor.Next;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, owner);
                cursor.EmitDelegate<Func<OriginalDreamBlockState, bool>>(block =>
                    block is DreamMoveBlock && !block.Visible);
                cursor.Emit(OpCodes.Brfalse, emission);
                foreach (var parameter in ((MethodReference)emission.Operand).Parameters) cursor.Emit(OpCodes.Pop);
                cursor.Emit(OpCodes.Pop);
                cursor.Emit(OpCodes.Br, emission.Next);
                cursor.Goto(emission, MoveType.After);
                matches++;
            }

            if (matches == 0)
                throw new InvalidOperationException("Original dream activation routine emission call missing.");
        }

        public void Dispose()
        {
            for (var i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
