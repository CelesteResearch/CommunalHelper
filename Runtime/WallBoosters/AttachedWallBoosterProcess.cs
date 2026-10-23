// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Entities.Platforms.WallBoosters;
using Celeste.Mods.CommunalHelper.Entities.WallBoosters;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Interaction.WallBooster;
using Celeste.Simulation.Gameplay.Player.Collision.Contacts;
using Celeste.Simulation.Gameplay.Player.Movement;
using Celeste.Simulation.Gameplay.Player.State;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.WallBoosters
{
    public sealed class AttachedWallBoosterProcess : IDisposable
    {
        private sealed class PlayerBoostState
        {
            public float CurrentSpeed;
            public float LiftSpeedTimer;
            public ISourceWallBooster LastWallBooster;
        }

        private readonly ISourceEngineContext engine;
        private static readonly ConditionalWeakTable<RoomPlayerSession, PlayerBoostState> players = new();
        private readonly List<IDisposable> hooks = new();
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

        public AttachedWallBoosterProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            try
            {
                hooks.Add(new ILHook(
                    typeof(AttachedWallBooster).GetConstructor(new[]
                        { typeof(ISourceEngineContext), typeof(EntityData), typeof(Vector2) }), InitializeWallBooster));
                hooks.Add(new ILHook(typeof(OriginalWallBoosterState).GetMethod("BuildSprite", InstancePrivate),
                    BuildSprite));
                hooks.Add(new Hook(typeof(ClimbSequence).GetMethod(nameof(ClimbSequence.Begin)),
                    (Action<Action<ClimbSequence>, ClimbSequence>)ClimbBegin));
                hooks.Add(new ILHook(typeof(ClimbSequence).GetMethod(nameof(ClimbSequence.Update)), ClimbUpdate));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.ClimbJump)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)ClimbJump));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.WallJump)),
                    (Action<Action<RoomPlayerSession, int>, RoomPlayerSession, int>)WallJump));
                hooks.Add(new ILHook(typeof(PlayerContacts).GetMethod(nameof(PlayerContacts.WallBoosterCheck)),
                    WallBoosterCheck));
                hooks.Add(new ILHook(
                    typeof(RoomPlayerSession).GetConstructor(new[]
                    {
                        typeof(Vector2), typeof(Celeste.Simulation.Gameplay.Player.Configuration.IPlayerAssistSettings),
                        typeof(Celeste.Input.Gameplay.ISourceGameInput), typeof(ISourceEngineContext),
                        typeof(Celeste.Simulation.Gameplay.Platforms.Collision.PlatformCollisionWorld),
                        typeof(IReadOnlyDictionary<string, string>),
                        typeof(Celeste.Simulation.Gameplay.Player.Presentation.Sprites.PlayerSpriteMode)
                    }), PlayerConstructor));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Update)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)PlayerUpdate));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(RoomPlayerSession player) => ReferenceEquals(player.EngineContext, engine);

        private PlayerBoostState State(RoomPlayerSession player) =>
            players.GetValue(player, static _ => new PlayerBoostState());

        private void InitializeWallBooster(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                i => i.Operand is MethodReference m && m.Name == ".ctor" &&
                     m.DeclaringType.FullName == typeof(OriginalWallBoosterState).FullName);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldarg_2);
            cursor.EmitDelegate<Action<AttachedWallBooster, ISourceEngineContext, EntityData>>((booster, owner, data) =>
            {
                if (ReferenceEquals(owner, engine))
                    booster.LegacyBoost = EntityAttributeReader.Bool(data.Values, "legacyBoost", true);
            });
        }

        private void BuildSprite(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                i => i.Operand is MethodReference m && m.Name == "Create" &&
                     m.DeclaringType.FullName == "Celeste.Graphics.Animation.ISpriteFactory");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<string, OriginalWallBoosterState, string>>((id, booster) =>
            {
                if (booster is AttachedWallBooster attached && !attached.LegacyBoost &&
                    ReferenceEquals(WallBoosterFields.Engine(booster), engine))
                    return id switch
                    {
                        "WallBoosterTop" => "BadAttachedWallBoosterTop",
                        "WallBoosterBottom" => "BadAttachedWallBoosterBottom",
                        _ => id
                    };
                return id;
            });
        }

        private void PlayerConstructor(ILContext il)
        {
            foreach (var instruction in il.Body.Instructions.ToArray())
            {
                if (instruction.OpCode != OpCodes.Ret)
                    continue;
                var cursor = new ILCursor(il);
                cursor.Goto(instruction, MoveType.Before);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<RoomPlayerSession>>(player =>
                {
                    if (Owns(player))
                    {
                        var data = State(player);
                        data.CurrentSpeed = 0f;
                        data.LiftSpeedTimer = 0f;
                    }
                });
            }
        }

        private void PlayerUpdate(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            original(player);
            if (!Owns(player))
                return;
            var data = State(player);
            if (data.LiftSpeedTimer > 0f)
                data.LiftSpeedTimer = ScalarMath.Approach(data.LiftSpeedTimer, 0f, engine.DeltaTime,
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
        }

        private void WallBoosterCheck(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<PlayerContacts>>(contacts =>
            {
                if (contacts.ActualOwner is RoomPlayerSession player && Owns(player))
                    State(player).LastWallBooster = null;
            });
            var booster = il.Body.Variables.Single(v => v.VariableType.FullName == typeof(ISourceWallBooster).FullName);
            cursor.GotoNext(MoveType.After,
                i => i.Operand is MethodReference m && m.Name == "Check" &&
                     m.DeclaringType.FullName == "Celeste.Physics.Collision.SourceEntityCollision");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, booster);
            cursor.EmitDelegate<Func<bool, PlayerContacts, ISourceWallBooster, bool>>((collides, contacts, wall) =>
            {
                if (collides && contacts.ActualOwner is RoomPlayerSession player && Owns(player))
                    State(player).LastWallBooster = wall;
                return collides;
            });
        }

        private RoomPlayerSession Player(ClimbSequence climb)
            => climb.World is RoomPlayerWorld world && Owns(world.SourcePlayer) ? world.SourcePlayer : null;

        private void ClimbBegin(Action<ClimbSequence> original, ClimbSequence climb)
        {
            original(climb);
            var player = Player(climb);
            if (player != null)
                State(player).LiftSpeedTimer = 0f;
        }

        private void ClimbUpdate(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, i => i.MatchLdfld<PlayerState>(nameof(PlayerState.WallBoosting)));
            cursor.GotoNext(MoveType.After, i => i.OpCode.FlowControl == FlowControl.Cond_Branch);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<ClimbSequence>>(climb =>
            {
                var player = Player(climb);
                if (player != null)
                    PlayerWallBoost(player);
            });
            cursor.GotoNext(MoveType.After,
                i => i.Operand is MethodReference m && m.Name == "set_LiftSpeed" &&
                     m.DeclaringType.FullName == typeof(IClimbWorld).FullName);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<ClimbSequence>>(climb =>
            {
                var player = Player(climb);
                if (player == null)
                    return;
                var data = State(player);
                data.CurrentSpeed = player.Speed.Y;
                data.LiftSpeedTimer = .25f;
            });
        }

        private void WallJump(Action<RoomPlayerSession, int> original, RoomPlayerSession player, int direction)
        {
            if (Owns(player))
                PlayerWallBoost(player);
            original(player, direction);
        }

        private void ClimbJump(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (Owns(player))
                PlayerWallBoost(player);
            original(player);
        }

        private void PlayerWallBoost(RoomPlayerSession player)
        {
            var data = State(player);
            if (data.LiftSpeedTimer > 0f && data.CurrentSpeed < 0f &&
                data.LastWallBooster is AttachedWallBooster { IceMode: false } booster)
            {
                if (player.LiftSpeed.Y >= 0f || !booster.LegacyBoost)
                    player.LiftSpeed += Vector2.UnitY *
                                        ((ISourceExtremaEnvironment)engine).ExtremaArithmetic.Max(data.CurrentSpeed,
                                            -80f);
                data.CurrentSpeed = 0f;
                data.LiftSpeedTimer = 0f;
            }
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--)
                hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
