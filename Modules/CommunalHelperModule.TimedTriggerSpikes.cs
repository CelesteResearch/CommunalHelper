using System;
using Celeste.Mods.CommunalHelper.Runtime.Hazards;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public sealed partial class CommunalHelperModule
    {
        private IDisposable timedTriggerSpikes;

        public override void Initialize()
        {
            timedTriggerSpikes = TimedSpikeCapabilities.Install(SourceEngine);
            InitializeCassetteBlocks();
        }

        private void UnloadTimedTriggerSpikes()
        {
            timedTriggerSpikes?.Dispose();
            timedTriggerSpikes = null;
        }
    }
}
