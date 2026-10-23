using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Game.Gameplay.Assists.Solids;
using Celeste.Game.Gameplay.Interaction.Springs;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Riding;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Responses;
using Celeste.Simulation.Gameplay.Player.Movement;
using Celeste.Simulation.Gameplay.Player.Presentation.Animation;
using Celeste.Simulation.Gameplay.Player.Runtime;
using Celeste.Simulation.Gameplay.Walls.Concealment;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamTunnels
{
    public sealed partial class DreamTunnelDashRuntime
    {
        private static readonly object stateChecksGate = new();
        private static readonly List<IDisposable> stateChecks = new();
        private static int stateCheckOwners;
        private bool stateChecksOwned;

        private static bool IsTunnel(RoomPlayerSession player)
            => player != null && contexts.TryGetValue(player.EngineContext, out var runtime) && runtime.loaded &&
               runtime.players.TryGetValue(player, out var state) && player.Machine.State == state.Index;

        private static int EquivalentState(RoomPlayerSession player, int current, int original)
            => IsTunnel(player) ? original : current;

        private void InstallStateChecks()
        {
            lock (stateChecksGate)
            {
                if (stateCheckOwners == 0)
                {
                    try
                    {
                        AddDirectStateChecks(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.IsRiding),
                            new[] { typeof(ISourceEntityCollider) }), 9, 1);
                        AddDirectStateChecks(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.IsRiding),
                            new[] { typeof(ISourceJumpThruCollider) }), 9, 1);
                        AddDirectStateChecks(PlayerMethod(nameof(RoomPlayerSession.BeforeUpTransition)), 5, 2);
                        AddDirectStateChecks(PlayerMethod(nameof(RoomPlayerSession.BeforeDownTransition)), 5, 1);
                        AddDirectStateChecks(typeof(FakeWallState).GetMethod(nameof(FakeWallState.Update)), 9, 1);
                        AddDirectStateChecks(typeof(OriginalSpringState).GetMethod("OnCollide",
                            BindingFlags.Instance | BindingFlags.NonPublic), 9, 1);
                        AddDirectStateChecks(
                            typeof(OriginalSolidAssistPolicy).GetMethod(nameof(OriginalSolidAssistPolicy
                                .AfterMovement)), 9, 1);
                        AddScopedStateChecks(typeof(PlayerFrameMotion).GetMethod(nameof(PlayerFrameMotion.Update)), 9,
                            1);
                        AddScopedStateChecks(typeof(PlayerMovement).GetMethod(nameof(PlayerMovement.Update)), 9, 2);
                        AddScopedStateChecks(typeof(PlayerAnimation).GetMethod(nameof(PlayerAnimation.Update)), 9, 1);
                        AddCollisionStateChecks(
                            typeof(PlayerCollisionResponse).GetMethod(nameof(PlayerCollisionResponse.Horizontal)));
                        AddCollisionStateChecks(
                            typeof(PlayerCollisionResponse).GetMethod(nameof(PlayerCollisionResponse.Vertical)));
                        InstallDashCoroutine();
                        InstallTransitionAndBounds();
                    }
                    catch (Exception failure)
                    {
                        var failures = new List<Exception>();
                        DisposeLeases(stateChecks, failures);
                        if (failures.Count != 0)
                        {
                            failures.Insert(0, failure);
                            throw new AggregateException(failures);
                        }

                        throw;
                    }
                }

                stateCheckOwners++;
                stateChecksOwned = true;
            }
        }

        private static void AddDirectStateChecks(MethodInfo method, int originalState, int expected)
        {
            if (method == null)
                throw new MissingMethodException("The actual dream tunnel state-check method is missing.");
            stateChecks.Add(new ILHook(method, il =>
            {
                var cursor = new ILCursor(il);
                int count = 0;
                while (cursor.TryGotoNext(MoveType.Before,
                           instruction => instruction.MatchLdfld<RoomPlayerSession>(nameof(RoomPlayerSession.Machine)),
                           instruction => instruction.Operand is MethodReference member && member.Name == "get_State",
                           instruction => instruction.MatchLdcI4(originalState)))
                {
                    var branch = cursor.Clone();
                    branch.Index += 3;
                    if (!branch.TryGotoNext(MoveType.Before, instruction =>
                            instruction.OpCode == OpCodes.Beq || instruction.OpCode == OpCodes.Beq_S ||
                            instruction.OpCode == OpCodes.Bne_Un || instruction.OpCode == OpCodes.Bne_Un_S))
                        throw new InvalidOperationException("The actual dream tunnel state-check branch is missing.");
                    object equalTarget = branch.Next.OpCode == OpCodes.Beq || branch.Next.OpCode == OpCodes.Beq_S
                        ? branch.Next.Operand
                        : branch.Next.Next;
                    var originalCheck = cursor.DefineLabel();
                    cursor.MoveAfterLabels();
                    cursor.Emit(OpCodes.Dup);
                    cursor.EmitDelegate<Func<RoomPlayerSession, bool>>(IsTunnel);
                    cursor.Emit(OpCodes.Brfalse, originalCheck);
                    cursor.Emit(OpCodes.Pop);
                    cursor.Emit(OpCodes.Br, equalTarget);
                    cursor.MarkLabel(originalCheck);
                    cursor.Goto(branch.Next, MoveType.After);
                    count++;
                }

                RequireCheckCount(method, expected, count);
            }));
        }

        private static void AddScopedStateChecks(MethodInfo method, int originalState, int expected)
        {
            stateChecks.Add(new ILHook(method, il =>
            {
                var cursor = new ILCursor(il);
                int count = 0;
                while (cursor.TryGotoNext(MoveType.Before, instruction => instruction.MatchLdcI4(originalState)))
                {
                    if (cursor.Next.Next == null || cursor.Next.Next.OpCode.FlowControl != FlowControl.Cond_Branch)
                    {
                        cursor.Index++;
                        continue;
                    }

                    cursor.MoveAfterLabels();
                    cursor.EmitDelegate<Func<int, int>>(current =>
                        EquivalentState(updatingPlayer, current, originalState));
                    cursor.Index++;
                    count++;
                }

                RequireCheckCount(method, expected, count);
            }));
        }

        private static void AddCollisionStateChecks(MethodInfo method)
        {
            stateChecks.Add(new ILHook(method, il =>
            {
                var cursor = new ILCursor(il);
                int count = 0;
                while (cursor.TryGotoNext(MoveType.Before,
                           instruction => instruction.Operand is MethodReference member && member.Name == "get_State",
                           instruction => instruction.MatchLdcI4(9),
                           instruction =>
                               instruction.OpCode == OpCodes.Bne_Un || instruction.OpCode == OpCodes.Bne_Un_S,
                           instruction => instruction.OpCode == OpCodes.Ret))
                {
                    cursor.Index++;
                    cursor.MoveAfterLabels();
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate<Func<int, PlayerCollisionResponse, int>>((current, owner) =>
                        EquivalentState(owner.Player, current, 9));
                    cursor.Index++;
                    count++;
                }

                // Original OnCollideH/V each have one state-9 collision early return. The former
                // second Vertical comparison belonged to MHH's ceiling extension, now owned by that Mod.
                RequireCheckCount(method, 1, count);
            }));
        }

        private static void RequireCheckCount(MethodInfo method, int expected, int actual)
        {
            if (actual != expected)
                throw new InvalidOperationException(
                    $"Dream tunnel state checks for {method.DeclaringType.FullName}.{method.Name}: expected {expected}, found {actual}.");
        }

        private void ReleaseStateChecks()
        {
            lock (stateChecksGate)
            {
                if (!stateChecksOwned) return;
                stateChecksOwned = false;
                stateCheckOwners--;
                if (stateCheckOwners != 0) return;
                var failures = new List<Exception>();
                DisposeLeases(stateChecks, failures);
                if (failures.Count != 0) throw new AggregateException(failures);
            }
        }
    }
}
