using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.Platforms.Dream;
using Celeste.Mods.CommunalHelper.Entities.DreamBlocks;
using Celeste.Simulation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamBlocks
{
    public sealed class CustomDreamBlockProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();

        private const BindingFlags DeclaredPrivate =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private CustomDreamBlockProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            try
            {
                hooks.Add(new Hook(typeof(OriginalDreamBlockState).GetMethod("OneUseDestroy", DeclaredPrivate),
                    (Action<Action<OriginalDreamBlockState>, OriginalDreamBlockState>)OneUseDestroy));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod("DreamDashBegin", DeclaredPrivate),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)DreamDashBegin));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod("DreamDashUpdate", DeclaredPrivate),
                    (Func<Func<RoomPlayerSession, int>, RoomPlayerSession, int>)DreamDashUpdate));
                hooks.Add(new ILHook(typeof(RoomPlayerSession).GetMethod("DreamDashEnd", DeclaredPrivate),
                    PatchDreamDashEnd));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine) => new CustomDreamBlockProcess(engine);

        private void OneUseDestroy(Action<OriginalDreamBlockState> original, OriginalDreamBlockState block)
        {
            if (block is CustomDreamBlock custom && ReferenceEquals(custom.EngineContext, engine) && custom.Collidable)
                custom.BeginShatter();
            else original(block);
        }

        private void DreamDashBegin(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            original(player);
            if (!ReferenceEquals(player.EngineContext, engine) ||
                player.CurrentDreamBlock is not CustomDreamBlock block) return;
            if (block.FeatherMode)
            {
                player.Stop(player.DreamLoopSound);
                player.Loop(player.DreamLoopSound,
                    "event:/CommunalHelperEvents/game/connectedDreamBlock/dreamblock_fly_travel");
            }

            if (block is IMovingDreamBlockEntry)
                player.Position += new Vector2(Math.Sign(player.Data.Direction.X), Math.Sign(player.Data.Direction.Y));
            player.Data.Motion.Normal.Speed = player.Data.Direction * block.DashSpeed;
        }

        private int DreamDashUpdate(Func<RoomPlayerSession, int> original, RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, engine) && player.CurrentDreamBlock is CustomDreamBlock block &&
                block.FeatherMode)
            {
                var vectors = ((ISourceVectorEnvironment)engine).VectorArithmetic;
                Vector2 input = vectors.SafeNormalize(player.Input.Aim.Value);
                if (input != Vector2.Zero)
                {
                    Vector2 direction = vectors.SafeNormalize(player.Data.Motion.Normal.Speed);
                    if (direction != Vector2.Zero)
                    {
                        if (vectors.Dot(input, direction) != -.8f)
                            direction = vectors.RotateTowards(direction, Vector2Geometry.Angle(input),
                                5f * engine.DeltaTime);
                        direction = CorrectJoystickPrecision(direction);
                        player.Data.Direction = direction;
                        player.Data.Motion.Normal.Speed = direction * block.DashSpeed;
                    }
                }
            }

            return original(player);
        }

        internal static Vector2 CorrectJoystickPrecision(Vector2 direction)
        {
            if (direction.X != 0 && Math.Abs(direction.X) < .001f)
            {
                direction.X = 0;
                direction.Y = Math.Sign(direction.Y);
            }
            else if (direction.Y != 0 && Math.Abs(direction.Y) < .001f)
            {
                direction.Y = 0;
                direction.X = Math.Sign(direction.X);
            }

            return direction;
        }

        private void PatchDreamDashEnd(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(instruction =>
                    instruction.MatchCallvirt<RoomPlayerWorld>(nameof(RoomPlayerWorld.RefillDash))))
                throw new InvalidOperationException("DreamDashEnd original refill call was not found.");
            if (cursor.Next.Next.OpCode != OpCodes.Pop)
                throw new InvalidOperationException("DreamDashEnd refill result must be discarded.");
            var pop = cursor.Next.Next;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<RoomPlayerSession, bool>>(player =>
                ReferenceEquals(player.EngineContext, engine) && player.CurrentDreamBlock is CustomDreamBlock block &&
                block.SkipExitDashRefill);
            cursor.Emit(OpCodes.Brtrue, pop);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
