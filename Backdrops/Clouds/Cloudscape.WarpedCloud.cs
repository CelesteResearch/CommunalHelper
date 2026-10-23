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
        private class WarpedCloud
        {
            private readonly Cloudscape parent;

            public PackedColor IdleColor { get; set; }
            private PackedColor targetColorA, targetColorB, flashColor;

            private float timer = 0f;
            private float flashDuration = 1f, flashTimer = 0f;
            private float intensity;

            private float oldPercent;

            private PackedColor color;

            public WarpedCloud(Cloudscape parent, PackedColor idleColor)
            {
                this.parent = parent;
                IdleColor = color = idleColor;
                timer = SourceRandomMath.Range(parent.Random, parent.lightningMinDelay, parent.lightningMaxDelay) *
                        SourceRandomMath.NextFloat(parent.Random);
            }

            public PackedColor CalculateColor(bool force = false)
            {
                float percent = flashTimer / flashDuration;
                if (oldPercent == percent && !force)
                    return color;

                float sin = ((float)Math.Sin(percent * 10) + 1) / 2f;
                PackedColor target = PackedColor.Lerp(targetColorA, targetColorB, sin);
                PackedColor lightning = PackedColor.Lerp(IdleColor, target,
                    parent.bounceIn(percent) * (1 - parent.cubeIn(percent)));
                color = intensity > 0
                    ? PackedColor.Lerp(lightning, flashColor, intensity * parent.expoIn(percent))
                    : lightning;

                oldPercent = percent;

                return color;
            }

            public void Update(bool allowLightning)
            {
                if (allowLightning)
                {
                    timer -= parent.Engine.DeltaTime;
                    if (timer <= 0)
                    {
                        timer = SourceRandomMath.Range(parent.Random, parent.lightningMinDelay,
                            parent.lightningMaxDelay);
                        flashColor = parent.lightningFlashColor;
                        flashTimer = flashDuration = SourceRandomMath.Range(parent.Random, parent.lightningMinDuration,
                            parent.lightningMaxDuration);
                        intensity =
                            ((ISourceLightningFlashEnvironment)parent.Engine).LightningFlashPolicy.AllowLightning
                                ? parent.lightningIntensity *
                                  parent.cubeIn(SourceRandomMath.NextFloat(parent.Random))
                                : 0;
                        targetColorA =
                            ColorArrayLerp(
                                SourceRandomMath.NextFloat(parent.Random) * (parent.lightningColors.Length - 1),
                                parent.lightningColors);
                        targetColorB =
                            ColorArrayLerp(
                                SourceRandomMath.NextFloat(parent.Random) * (parent.lightningColors.Length - 1),
                                parent.lightningColors);
                    }
                }

                if (flashTimer > 0)
                    flashTimer = ScalarMath.Approach(flashTimer, 0, parent.Engine.DeltaTime);
            }
        }
    }
}
