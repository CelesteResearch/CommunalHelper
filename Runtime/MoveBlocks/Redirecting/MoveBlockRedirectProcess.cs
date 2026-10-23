// Copyright (c) 2020 Flynn Swainston-Calcutt. Licensed under the MIT License.

using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.MoveBlocks.Redirecting
{
    public sealed class MoveBlockRedirectProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private Hook dataConstructor;
        private Hook positionConstructor;
        private Hook breakParticles;
        private ILHook movementSound;

        private MoveBlockRedirectProcess(ISourceEngineContext engine) => this.engine = engine;

        public static MoveBlockRedirectProcess Install(ISourceEngineContext engine)
        {
            var process = new MoveBlockRedirectProcess(engine);
            try
            {
                process.dataConstructor = new Hook(typeof(MoveBlockState).GetConstructor(new[]
                        { typeof(EntityData), typeof(Vector2), typeof(ISourceSolidWorldFactory) }),
                    (Action<Action<MoveBlockState, EntityData, Vector2, ISourceSolidWorldFactory>, MoveBlockState,
                        EntityData, Vector2, ISourceSolidWorldFactory>)process.Construct);
                process.positionConstructor = new Hook(typeof(MoveBlockState).GetConstructor(new[]
                    {
                        typeof(Vector2), typeof(int), typeof(int), typeof(MoveBlockDirection), typeof(bool),
                        typeof(bool), typeof(ISourceSolidWorld)
                    }),
                    (Action<Action<MoveBlockState, Vector2, int, int, MoveBlockDirection, bool, bool, ISourceSolidWorld>
                        ,
                        MoveBlockState, Vector2, int, int, MoveBlockDirection, bool, bool, ISourceSolidWorld>)process
                        .Construct);
                process.breakParticles = new Hook(
                    typeof(MovingBlockState).GetMethod(nameof(MovingBlockState.BreakParticles)),
                    (Action<Action<MovingBlockState>, MovingBlockState>)process.ResetAfterBreakParticles);
                MethodInfo core = typeof(MovingBlockState).GetMethod("ControllerCore",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Type stateMachine = core.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
                process.movementSound = new ILHook(
                    stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic), il =>
                    {
                        var cursor = new ILCursor(il);
                        if (!cursor.TryGotoNext(MoveType.After,
                                instruction => instruction.MatchLdstr("event:/game/04_cliffside/arrowblock_move")))
                            throw new InvalidOperationException("The source moving-block sound call was not found.");
                        // The hook is scoped by the actual iterator's owner, so another engine retains its original event.
                        FieldInfo owner = stateMachine
                            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            .Single(field => field.FieldType == typeof(MovingBlockState));
                        cursor.Emit(OpCodes.Ldarg_0);
                        cursor.Emit(OpCodes.Ldfld, owner);
                        cursor.EmitDelegate<Func<string, MovingBlockState, string>>((original, block) =>
                            ReferenceEquals(block.EngineContext, engine)
                                ? "event:/CommunalHelperEvents/game/redirectMoveBlock/arrowblock_move"
                                : original);
                    });
                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        private void Construct(Action<MoveBlockState, EntityData, Vector2, ISourceSolidWorldFactory> original,
            MoveBlockState block, EntityData data, Vector2 offset, ISourceSolidWorldFactory worlds)
        {
            original(block, data, offset, worlds);
            Attach(block);
        }

        private void Construct(
            Action<MoveBlockState, Vector2, int, int, MoveBlockDirection, bool, bool, ISourceSolidWorld> original,
            MoveBlockState block, Vector2 position, int width, int height, MoveBlockDirection direction, bool canSteer,
            bool fast,
            ISourceSolidWorld world)
        {
            original(block, position, width, height, direction, canSteer, fast, world);
            Attach(block);
        }

        private void Attach(MoveBlockState block)
        {
            if (block.GetType() == typeof(MoveBlockState) && ReferenceEquals(block.EngineContext, engine))
                block.OwnedComponents.Add(new MoveBlockRedirectable(new VanillaMoveBlockRedirectAdapter(block)));
        }

        private void ResetAfterBreakParticles(Action<MovingBlockState> original, MovingBlockState block)
        {
            original(block);
            if (ReferenceEquals(block.EngineContext, engine))
                block.OfType<MoveBlockRedirectable>().FirstOrDefault()?.ResetBlock();
        }

        public void Dispose()
        {
            movementSound?.Dispose();
            movementSound = null;
            breakParticles?.Dispose();
            breakParticles = null;
            positionConstructor?.Dispose();
            positionConstructor = null;
            dataConstructor?.Dispose();
            dataConstructor = null;
        }
    }
}
