using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.CommunalHelper.Backdrops.Clouds;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.CommunalHelper.Triggers.Clouds
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Triggers.CloudscapeColorTransitionTrigger")]
    [CustomEntity("CommunalHelper/CloudscapeColorTransitionTrigger")]
    public class CloudscapeColorTransitionTrigger : SourceTriggerEntityState
    {
        public enum Mode
        {
            TopToBottom,
            BottomToTop,
            LeftToRight,
            RightToLeft,
        }

        private readonly string tag;
        private readonly Mode mode;
        private readonly PackedColor[] from, to;
        private readonly PackedColor bgFrom, bgTo;

        private float prevLerp;

        public CloudscapeColorTransitionTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            tag = data.Attr("tag");
            mode = data.Enum("mode", Mode.LeftToRight);
            from = data.Attr("colorsFrom", "6d8adaff,aea0c1ff,d9cbbcff")
                .Split(',')
                .Select(str => Cloudscape.HexToColorWithAlpha(str.Trim()))
                .ToArray();
            to = data.Attr("colorsTo", "ff0000ff,00ff00ff,0000ffff")
                .Split(',')
                .Select(str => Cloudscape.HexToColorWithAlpha(str.Trim()))
                .ToArray();
            bgFrom = Cloudscape.HexToColorWithAlpha(data.Attr("bgFrom", "4f9af7ff").Trim());
            bgTo = Cloudscape.HexToColorWithAlpha(data.Attr("bgTo", "000000ff").Trim());
        }

        public override void OnStay(RoomPlayerSession player)
        {
            base.OnStay(player);

            if (Scene is not OriginalLevelSceneRoot level)
                return;

            float lerp = mode switch
            {
                Mode.LeftToRight => ScalarMath.ClampedMap(player.X, Left, Right),
                Mode.RightToLeft => 1f - ScalarMath.ClampedMap(player.X, Left, Right),
                Mode.TopToBottom => ScalarMath.ClampedMap(player.Y, Top, Bottom),
                Mode.BottomToTop => 1f - ScalarMath.ClampedMap(player.Y, Top, Bottom),
                _ => 0f,
            };

            if (prevLerp == lerp)
                return;
            prevLerp = lerp;

            PackedColor bg = PackedColor.Lerp(bgFrom, bgTo, lerp);

            if (string.IsNullOrEmpty(tag))
            {
                level.Background.Get<Cloudscape>()?.ConfigureColors(bg, from, to, lerp);
            }
            else
            {
                foreach (Cloudscape cloudscape in level.Background.GetEach<Cloudscape>(tag)
                             .Concat(level.Foreground.GetEach<Cloudscape>(tag)))
                    cloudscape.ConfigureColors(bg, from, to, lerp);
            }
        }
    }
}
