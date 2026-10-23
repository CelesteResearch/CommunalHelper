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
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Triggers.CloudscapeLightningConfigurationTrigger")]
    [CustomEntity("CommunalHelper/CloudscapeLightningConfigurationTrigger")]
    public class CloudscapeLightningConfigurationTrigger : SourceTriggerEntityState
    {
        private readonly string tag;
        private readonly bool enable;
        private readonly PackedColor[] lightningColors;
        private readonly PackedColor lightningFlashColor;
        private readonly float lightningMinDelay, lightningMaxDelay;
        private readonly float lightningMinDuration, lightningMaxDuration;
        private readonly float lightningIntensity;

        public CloudscapeLightningConfigurationTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset, data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            tag = data.Attr("tag");
            enable = data.Bool("enable", true);
            lightningColors = data.Attr("lightningColors", "384bc8,7a50d0,c84ddd,3397e2")
                .Split(',')
                .Select(str => SourceHexColor.HexToColor(str.Trim()))
                .ToArray();
            lightningFlashColor = SourceHexColor.HexToColor(data.Attr("lightningFlashColor").Trim());
            lightningMinDelay =
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic.Max(data.Float("lightningMinDelay", 5.0f), 0);
            lightningMaxDelay =
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic.Max(data.Float("lightningMaxDelay", 40.0f), 0);
            lightningMinDuration =
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic.Max(data.Float("lightningMinDuration", 0.5f), 0);
            lightningMaxDuration =
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic.Max(data.Float("lightningMaxDuration", 1.0f), 0);
            lightningIntensity = ScalarMath.Clamp(data.Float("lightningIntensity", 0.5f), 0f, 1f);
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);

            if (Scene is not OriginalLevelSceneRoot level)
                return;

            if (string.IsNullOrEmpty(tag))
            {
                level.Background.Get<Cloudscape>()?.ConfigureLightning(
                    enable, lightningColors, lightningFlashColor,
                    lightningMinDelay, lightningMaxDelay,
                    lightningMinDuration, lightningMaxDuration,
                    lightningIntensity
                );
            }
            else
            {
                foreach (Cloudscape cloudscape in level.Background.GetEach<Cloudscape>(tag)
                             .Concat(level.Foreground.GetEach<Cloudscape>(tag)))
                    cloudscape.ConfigureLightning(
                        enable, lightningColors, lightningFlashColor,
                        lightningMinDelay, lightningMaxDelay,
                        lightningMinDuration, lightningMaxDuration,
                        lightningIntensity
                    );
            }
        }
    }
}
