using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
    public sealed class ConnectedDreamBlockProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();

        private ConnectedDreamBlockProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            try
            {
                hooks.Add(new Hook(
                    typeof(OriginalDreamBlockState).GetMethod(nameof(OriginalDreamBlockState.FootstepRipple)),
                    (Action<Action<OriginalDreamBlockState, Vector2>, OriginalDreamBlockState, Vector2>)
                    FootstepRipple));
                AddRoutine(typeof(OriginalDreamBlockState), nameof(OriginalDreamBlockState.Activate), false);
                AddRoutine(typeof(EverestDreamBlockState), nameof(EverestDreamBlockState.Deactivate), false);
                AddRoutine(typeof(EverestDreamBlockState), nameof(EverestDreamBlockState.FastActivate), true);
                AddRoutine(typeof(EverestDreamBlockState), nameof(EverestDreamBlockState.FastDeactivate), true);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine) => new ConnectedDreamBlockProcess(engine);

        private void AddRoutine(Type type, string name, bool fast)
        {
            MethodInfo routine = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Type iterator = routine.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            FieldInfo owner = iterator.GetField("<>4__this", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo moveNext = iterator.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
            hooks.Add(new ILHook(moveNext, il => PatchRoutine(il, owner, fast)));
        }

        private void PatchRoutine(ILContext il, FieldInfo owner, bool fast)
        {
            var cursor = new ILCursor(il);
            object target;
            if (fast)
            {
                var emissions = il.Instrs.Where(instruction =>
                    instruction.Operand is MethodReference reference && reference.Name == "Emit").ToArray();
                if (emissions.Length != 4)
                    throw new InvalidOperationException("Fast dream routine must have four original emission calls.");
                target = emissions[3].Next;
                if (!cursor.TryGotoNext(MoveType.AfterLabel,
                        instruction => instruction.MatchLdarg(0),
                        instruction => instruction.Operand is FieldReference field && field.Name.Contains("level"),
                        instruction => instruction.Operand is FieldReference particleField &&
                                       particleField.Name == "ParticlesFG"))
                    throw new InvalidOperationException("Fast dream routine particle entry was not found.");
            }
            else
            {
                if (!cursor.TryGotoNext(MoveType.After,
                        instruction =>
                            instruction.Operand is MethodReference reference && reference.Name == "OnInterval",
                        instruction =>
                            instruction.OpCode == OpCodes.Brfalse || instruction.OpCode == OpCodes.Brfalse_S))
                    throw new InvalidOperationException("Slow dream routine particle interval branch was not found.");
                target = cursor.Prev.Operand;
            }

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, owner);
            cursor.EmitDelegate<Func<OriginalDreamBlockState, bool>>(block =>
            {
                if (block is not ConnectedDreamBlock connected || !connected.OwnedBy(engine)) return false;
                if (fast) connected.SpawnFastRoutineParticles();
                else connected.SpawnSlowRoutineParticles();
                return true;
            });
            cursor.Emit(OpCodes.Brtrue, target);
        }

        private void FootstepRipple(Action<OriginalDreamBlockState, Vector2> original, OriginalDreamBlockState block,
            Vector2 position)
        {
            if (block is ConnectedDreamBlock connected && connected.OwnedBy(engine))
                connected.ConnectedFootstepRipple(position);
            else original(block, position);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
