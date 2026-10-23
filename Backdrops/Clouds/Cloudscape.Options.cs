using System;
using Celeste.Game.Execution.Graphics;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Celeste.Core.Scheduling;
using Celeste.Core.Randomness;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Settings.Lightning;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Binary.Source;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Graphics.Display.Colors;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.Rendering;
using Celeste.Simulation.Presentation.Backdrops.Runtime;

namespace Celeste.Mods.CommunalHelper.Backdrops.Clouds
{
    public partial class Cloudscape
    {
        public enum ZoomBehaviors
        {
            StaySame,
            Adjust,
        }

        public class Options
        {
            public int Seed { get; } = 0;

            public PackedColor[] Colors { get; } = new[]
            {
                HexToColorWithAlpha("6d8adaff"),
                HexToColorWithAlpha("aea0c1ff"),
                HexToColorWithAlpha("d9cbbcff"),
            };

            public PackedColor Sky { get; } = HexToColorWithAlpha("4f9af7ff");

            public float InnerRadius { get; } = 40.0f;
            public float OuterRadius { get; } = 400.0f;
            public int Count { get; } = 24;

            public bool Lightning { get; } = false;

            public PackedColor[] LightningColors { get; } = new[]
            {
                SourceHexColor.HexToColor("384bc8"),
                SourceHexColor.HexToColor("7a50d0"),
                SourceHexColor.HexToColor("c84ddd"),
                SourceHexColor.HexToColor("3397e2"),
            };

            public PackedColor LightningFlashColor { get; } = PackedColor.White;

            public float LightningMinDelay { get; } = 5.0f;
            public float LightningMaxDelay { get; } = 40.0f;
            public float LightningMinDuration { get; } = 0.5f;
            public float LightningMaxDuration { get; } = 1.0f;
            public float LightningIntensity { get; } = 0.4f;

            public Vector2 Offset { get; } = Vector2.Zero;
            public Vector2 Parallax { get; } = Vector2.One * 0.05f;

            public float InnerDensity { get; } = 1.0f;
            public float OuterDensity { get; } = 1.0f;
            public float InnerRotation { get; } = 0.002f;
            public float OuterRotation { get; } = 0.2f;
            public float RotationExponent { get; } = 2.0f;

            public bool HasBackgroundColor { get; } = true;
            public bool Additive { get; } = false;
            public float BufferAlpha { get; } = 1.0f;

            public ZoomBehaviors ZoomBehavior { get; } = ZoomBehaviors.StaySame;

            public Options()
            {
            }

            public Options(BinaryPackerElement child)
            {
                Seed = child.Attr("seed").GetHashCode();

                Colors = child.Attr("colors", "6d8adaff,aea0c1ff,d9cbbcff")
                    .Split(',')
                    .Select(str => HexToColorWithAlpha(str.Trim()))
                    .ToArray();
                Sky = HexToColorWithAlpha(child.Attr("bgColor", "4f9af7ff").Trim());

                InnerRadius = Math.Max(child.AttrFloat("innerRadius", 40f), 10);
                OuterRadius = child.AttrFloat("outerRadius", 400f);
                Count = int.Parse(child.Attr("rings", "24"));

                Lightning = child.AttrBool("lightning", false);
                LightningColors = child.Attr("lightningColors", "384bc8,7a50d0,c84ddd,3397e2")
                    .Split(',')
                    .Select(str => SourceHexColor.HexToColor(str.Trim()))
                    .ToArray();
                LightningFlashColor = SourceHexColor.HexToColor(child.Attr("lightningFlashColor").Trim());

                LightningMinDelay = Math.Max(child.AttrFloat("lightningMinDelay", 5.0f), 0);
                LightningMaxDelay = Math.Max(child.AttrFloat("lightningMaxDelay", 40.0f), 0);
                LightningMinDuration = Math.Max(child.AttrFloat("lightningMinDuration", 0.5f), 0);
                LightningMaxDuration = Math.Max(child.AttrFloat("lightningMaxDuration", 1.0f), 0);
                LightningIntensity = ScalarMath.Clamp(child.AttrFloat("lightningIntensity", 0.5f), 0f, 1f);

                Offset = new Vector2(child.AttrFloat("offsetX"), child.AttrFloat("offsetY"));
                Parallax = new Vector2(child.AttrFloat("parallaxX", 0.05f), child.AttrFloat("parallaxY", 0.05f));

                InnerDensity = ScalarMath.Clamp(child.AttrFloat("innerDensity", 1f), 0f, 2f);
                OuterDensity = ScalarMath.Clamp(child.AttrFloat("outerDensity", 1f), 0f, 2f);
                InnerRotation = child.AttrFloat("innerRotation", 0.002f);
                OuterRotation = child.AttrFloat("outerRotation", 0.2f);
                RotationExponent = Math.Max(child.AttrFloat("rotationExponent", 2f), 1f);

                HasBackgroundColor = child.AttrBool("hasBackgroundColor", true);
                Additive = child.AttrBool("additive", false);
                BufferAlpha = child.AttrFloat("alpha", 1f);
                ZoomBehavior =
                    Enum.TryParse<ZoomBehaviors>(child.Attr("zoomBehavior", "StaySame"), true, out var behavior)
                        ? behavior
                        : ZoomBehaviors.StaySame;
            }
        }
    }
}
