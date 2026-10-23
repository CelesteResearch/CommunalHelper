using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Gameplay.Player.State;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamTunnels
{
    public sealed partial class DreamTunnelDashRuntime
    {
        private static DreamTunnelDashRuntime RuntimeFor(DashSequence sequence)
        {
            if (!dataOwners.TryGetValue(sequence.Data, out var player) ||
                !contexts.TryGetValue(player.EngineContext, out var runtime) || !runtime.loaded) return null;
            return runtime;
        }

        private static void InstallDashCoroutine()
        {
            var iterator = typeof(DashSequence).GetMethod(nameof(DashSequence.Coroutine))
                .GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            var ownerField = iterator.GetField("<>4__this",
                                 BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                             ?? throw new MissingFieldException(iterator.FullName, "<>4__this");
            var moveNext = iterator.GetMethod("MoveNext",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            stateChecks.Add(new ILHook(moveNext, il =>
            {
                var cursor = new ILCursor(il);
                if (!cursor.TryGotoNext(MoveType.After,
                        instruction => instruction.MatchCall<DashSequence>(nameof(DashSequence.CallEvents))))
                    throw new InvalidOperationException("The actual dash coroutine CallEvents point is missing.");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, ownerField);
                cursor.EmitDelegate<Action<DashSequence>>(sequence =>
                {
                    var runtime = RuntimeFor(sequence);
                    if (runtime != null && runtime.Configuration.AllowDashCancels)
                        runtime.StartAttacking(dataOwners.GetValue(sequence.Data, _ =>
                            throw new InvalidOperationException(
                                "The actual dash player is missing.")), sequence.Data.Direction);
                });
                if (!cursor.TryGotoNext(MoveType.After,
                        instruction => instruction.MatchLdfld<PlayerState>(nameof(PlayerState.OnGround))))
                    throw new InvalidOperationException(
                        "The actual dash coroutine grounded diagonal branch is missing.");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, ownerField);
                cursor.EmitDelegate<Func<bool, DashSequence, bool>>((onGround, sequence) =>
                    onGround && !(RuntimeFor(sequence)?.attacking ?? false));
            }));
        }
    }
}
