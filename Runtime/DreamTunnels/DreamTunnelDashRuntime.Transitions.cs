using System;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Execution.Levels;
using Celeste.Physics.Collision;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamTunnels
{
    public sealed partial class DreamTunnelDashRuntime
    {
        private static void InstallTransitionAndBounds()
        {
            stateChecks.Add(new ILHook(PlayerMethod(nameof(RoomPlayerSession.TransitionTo)), il =>
            {
                var cursor = new ILCursor(il);
                if (!cursor.TryGotoNext(MoveType.Before,
                        instruction =>
                            instruction.MatchCall<SourceActorEntityState>(nameof(SourceActorEntityState.MoveTowardsX))))
                    throw new InvalidOperationException("The actual player transition MoveTowardsX call is missing.");
                cursor.Remove();
                cursor.EmitDelegate<Action<SourceActorEntityState, float, float, SourceCollisionHandler>>(
                    MoveTransitionX);
                if (!cursor.TryGotoNext(MoveType.Before,
                        instruction =>
                            instruction.MatchCall<SourceActorEntityState>(nameof(SourceActorEntityState.MoveTowardsY))))
                    throw new InvalidOperationException("The actual player transition MoveTowardsY call is missing.");
                cursor.Remove();
                cursor.EmitDelegate<Action<SourceActorEntityState, float, float, SourceCollisionHandler>>(
                    MoveTransitionY);
            }));
            stateChecks.Add(new ILHook(
                typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.EnforceBounds)), il =>
                {
                    var cursor = new ILCursor(il);
                    if (!cursor.TryGotoNext(MoveType.After,
                            instruction => instruction.MatchLdfld<OriginalLevelSceneRoot>("TransitionCoroutine"),
                            instruction => instruction.OpCode == OpCodes.Brfalse ||
                                           instruction.OpCode == OpCodes.Brfalse_S,
                            instruction => instruction.OpCode == OpCodes.Ret))
                        throw new InvalidOperationException(
                            "The actual level EnforceBounds transition return point is missing.");
                    cursor.MoveAfterLabels();
                    var proceed = cursor.DefineLabel();
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.Emit(OpCodes.Ldarg_1);
                    cursor.EmitDelegate<Func<OriginalLevelSceneRoot, RoomPlayerSession, bool>>(TunnelOutsideBounds);
                    cursor.Emit(OpCodes.Brfalse, proceed);
                    cursor.Emit(OpCodes.Ret);
                    cursor.MarkLabel(proceed);
                    if (!cursor.TryGotoNext(MoveType.After,
                            instruction => instruction.Operand is MethodReference member &&
                                           member.Name == "PlayerCollidesWithSolidAt"))
                        throw new InvalidOperationException("The actual level downward solid check is missing.");
                    cursor.Emit(OpCodes.Ldarg_1);
                    cursor.EmitDelegate<Func<bool, RoomPlayerSession, bool>>((collides, player) =>
                        collides && !IsTunnel(player));
                }));
        }

        private static void MoveTransitionX(SourceActorEntityState actor, float target, float maximumMove,
            SourceCollisionHandler onCollide)
        {
            if (actor is RoomPlayerSession player && IsTunnel(player))
            {
                float to = ScalarMath.Approach(player.ExactPosition.X, target, maximumMove,
                    ((ISourceExtremaEnvironment)player.EngineContext).ExtremaArithmetic);
                float move = (float)((double)to - player.Position.X - player.PositionRemainder.X);
                player.NaiveMove(Vector2.UnitX * move);
            }
            else actor.MoveTowardsX(target, maximumMove, onCollide);
        }

        private static void MoveTransitionY(SourceActorEntityState actor, float target, float maximumMove,
            SourceCollisionHandler onCollide)
        {
            if (actor is RoomPlayerSession player && IsTunnel(player))
            {
                float to = ScalarMath.Approach(player.ExactPosition.Y, target, maximumMove,
                    ((ISourceExtremaEnvironment)player.EngineContext).ExtremaArithmetic);
                float move = (float)((double)to - player.Position.Y - player.PositionRemainder.Y);
                player.NaiveMove(Vector2.UnitY * move);
            }
            else actor.MoveTowardsY(target, maximumMove, onCollide);
        }

        private static bool TunnelOutsideBounds(OriginalLevelSceneRoot level, RoomPlayerSession player)
        {
            if (!IsTunnel(player)) return false;
            var runtime = contexts.GetValue(player.EngineContext, _ => throw new InvalidOperationException(
                "The actual dream tunnel owner is missing."));
            if (runtime.Configuration.AllowTransitions) return false;
            var bounds = level.Bounds;
            if (player.Right <= bounds.Right && player.Left >= bounds.Left && player.Top >= bounds.Top &&
                player.Bottom <= bounds.Bottom) return false;
            runtime.DreamDashDie(player, player.Position);
            return true;
        }

        private void InstallLevelLifecycle()
        {
            hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Reload)),
                (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)((original, level) =>
                {
                    if (ReferenceEquals(level.EngineContext, Engine))
                    {
                        ChargeCount = 0;
                        attacking = false;
                    }

                    original(level);
                })));
            hooks.Add(new Hook(
                typeof(OriginalLevelLoaderScene).GetMethod("StartLevel",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                (Action<Action<OriginalLevelLoaderScene>, OriginalLevelLoaderScene>)((original, loader) =>
                {
                    if (ReferenceEquals(loader.EngineContext, Engine))
                    {
                        ChargeCount = 0;
                        attacking = false;
                    }

                    original(loader);
                })));
        }
    }
}
