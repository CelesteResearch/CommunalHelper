using Celeste.Mods.CommunalHelper.Runtime.DreamTunnels;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public sealed partial class CommunalHelperModule
    {
        private DreamTunnelDashRuntime dreamTunnels;

        private void LoadDreamTunnels()
        {
            dreamTunnels = DreamTunnelDashRuntime.For(SourceEngine);
            dreamTunnels.Bind(() => Settings, () => Session);
            dreamTunnels.Load();
        }

        private void UnloadDreamTunnels()
        {
            var runtime = dreamTunnels;
            dreamTunnels = null;
            runtime?.Dispose();
        }

        private void InitializeDreamTunnelParticles()
            => DreamTunnelDashRuntime.For(SourceEngine).InitializeParticles();
    }
}
