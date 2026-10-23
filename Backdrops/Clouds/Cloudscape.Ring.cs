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
        private class Ring
        {
            public float Lerp { get; }
            private readonly WarpedCloud[] clouds;

            public Ring(float lerp, WarpedCloud[] clouds)
            {
                Lerp = lerp;
                this.clouds = clouds;
            }

            public void ApplyIdleColor(PackedColor color, PackedColor[] array)
            {
                for (int i = 0; i < clouds.Length; i++)
                {
                    var cloud = clouds[i];
                    cloud.IdleColor = color;
                    array[i] = cloud.CalculateColor(force: true);
                }
            }
        }
    }
}
