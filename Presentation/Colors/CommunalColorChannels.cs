// SPDX-License-Identifier: MIT

using Celeste.Core.Display;

namespace Celeste.Mods.CommunalHelper.Presentation.Colors
{
    internal static class CommunalColorChannels
    {
        internal static PackedColor Multiply(PackedColor color, PackedColor other)
        {
            color.R = (byte)(color.R * other.R / 256f);
            color.G = (byte)(color.G * other.G / 256f);
            color.B = (byte)(color.B * other.B / 256f);
            color.A = (byte)(color.A * other.A / 256f);
            return color;
        }
    }
}
