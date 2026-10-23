using System;
using Celeste.Core.Display;
using Celeste.Graphics.Display.Colors;

namespace Celeste.Mods.CommunalHelper.Backdrops.Clouds
{
    public partial class Cloudscape
    {
        private static PackedColor ColorArrayLerp(float lerp, params PackedColor[] colors)
        {
            float m = (lerp % colors.Length + colors.Length) % colors.Length;
            int from = (int)Math.Floor(m);
            int to = ((from + 1) % colors.Length + colors.Length) % colors.Length;
            return PackedColor.Lerp(colors[from], colors[to], m - from);
        }

        internal static PackedColor HexToColorWithAlpha(string hex)
        {
            int start = hex.Length > 0 && hex[0] == '#' ? 1 : 0;
            if (hex.Length - start is not (6 or 8)) return PackedColor.White;
            int r = SourceHexColor.HexToByte(hex[start]) * 16 + SourceHexColor.HexToByte(hex[start + 1]);
            int g = SourceHexColor.HexToByte(hex[start + 2]) * 16 + SourceHexColor.HexToByte(hex[start + 3]);
            int b = SourceHexColor.HexToByte(hex[start + 4]) * 16 + SourceHexColor.HexToByte(hex[start + 5]);
            var color = new PackedColor(r, g, b);
            return hex.Length - start == 8
                ? color * ((SourceHexColor.HexToByte(hex[start + 6]) * 16 + SourceHexColor.HexToByte(hex[start + 7])) /
                           255f)
                : color;
        }
    }
}
