// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.Platforms.WallBoosters;
using Celeste.Graphics.Animation;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Environment.Core;

namespace Celeste.Mods.CommunalHelper.Runtime.WallBoosters
{
    internal static class WallBoosterFields
    {
        internal static readonly Func<OriginalWallBoosterState, List<Sprite>> Tiles = Read<List<Sprite>>("tiles");
        internal static readonly Func<OriginalWallBoosterState, bool> NotCoreMode = Read<bool>("notCoreMode");

        internal static readonly Func<OriginalWallBoosterState, ISourceEngineContext> Engine =
            Read<ISourceEngineContext>("engine");

        internal static readonly Action<OriginalWallBoosterState, object> EntityAdded = BaseAdded();
        internal static readonly Action<OriginalWallBoosterState, RoomCoreMode> ChangeMode = ModeChange();

        private static Func<OriginalWallBoosterState, T> Read<T>(string name)
        {
            var owner = typeof(OriginalWallBoosterState);
            var field = owner.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            var method = new DynamicMethod("ReadWallBooster" + name, typeof(T), new[] { owner }, owner, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            return (Func<OriginalWallBoosterState, T>)method.CreateDelegate(typeof(Func<OriginalWallBoosterState, T>));
        }

        private static Action<OriginalWallBoosterState, object> BaseAdded()
        {
            var owner = typeof(OriginalWallBoosterState);
            var method = new DynamicMethod("WallBoosterBaseEntityAdded", typeof(void), new[] { owner, typeof(object) },
                owner, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(SceneEntityState).GetMethod(nameof(SceneEntityState.Added)));
            il.Emit(OpCodes.Ret);
            return (Action<OriginalWallBoosterState, object>)method.CreateDelegate(
                typeof(Action<OriginalWallBoosterState, object>));
        }

        private static Action<OriginalWallBoosterState, RoomCoreMode> ModeChange()
        {
            var owner = typeof(OriginalWallBoosterState);
            var target = owner.GetMethod("OnChangeMode", BindingFlags.NonPublic | BindingFlags.Instance);
            var method = new DynamicMethod("WallBoosterChangeMode", typeof(void), new[] { owner, typeof(RoomCoreMode) },
                owner, true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, target);
            il.Emit(OpCodes.Ret);
            return (Action<OriginalWallBoosterState, RoomCoreMode>)method.CreateDelegate(
                typeof(Action<OriginalWallBoosterState, RoomCoreMode>));
        }
    }
}
