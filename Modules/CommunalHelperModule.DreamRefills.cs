using System;
using Celeste.Mods.CommunalHelper.Persistence;
using Celeste.Mods.CommunalHelper.Presentation.Refills;
using Celeste.Mods.CommunalHelper.Runtime.Refills;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public sealed partial class CommunalHelperModule
    {
        private DashStateRefillHooks dreamRefills;
        public override Type SettingsType => typeof(CommunalHelperSettings);
        public CommunalHelperSettings Settings => (CommunalHelperSettings)_Settings;

        public override void SaveSettings()
        {
            Settings.SavingSettings = true;
            base.SaveSettings();
            Settings.SavingSettings = false;
        }

        private void LoadDreamRefills() => dreamRefills = new DashStateRefillHooks(SourceEngine);

        private void UnloadDreamRefills()
        {
            dreamRefills?.Dispose();
            dreamRefills = null;
        }

        private void InitializeDreamRefillParticles()
            => DreamTunnelRefillParticles.For(SourceEngine).InitializeParticles();
    }
}
