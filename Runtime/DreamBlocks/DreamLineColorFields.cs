// SPDX-License-Identifier: MIT

using System;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Core.Display;
using Celeste.Game.Entities.Platforms.Dream;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamBlocks
{
    internal static class DreamLineColorFields
    {
        private static readonly Func<PackedColor> getter;
        private static readonly Action<PackedColor> setter;

        static DreamLineColorFields()
        {
            var owner = typeof(OriginalDreamBlockState);
            var field = owner.GetField("activeLineColor", BindingFlags.NonPublic | BindingFlags.Static);
            var read = new DynamicMethod("ReadDreamActiveLineColor", typeof(PackedColor), Type.EmptyTypes, owner, true);
            var il = read.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ret);
            getter = (Func<PackedColor>)read.CreateDelegate(typeof(Func<PackedColor>));
            var write = new DynamicMethod("WriteDreamActiveLineColor", typeof(void), new[] { typeof(PackedColor) },
                owner, true);
            il = write.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);
            setter = (Action<PackedColor>)write.CreateDelegate(typeof(Action<PackedColor>));
        }

        internal static PackedColor Get() => getter();
        internal static void Set(PackedColor color) => setter(color);
    }
}
