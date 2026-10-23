namespace Celeste.Mods.CommunalHelper.Configuration.DreamTunnels
{
    public struct DreamTunnelDashConfigurationChanges
    {
        public bool? NewAllowRedirect;
        public bool? NewAllowSameDirectionRedirect;
        public float? NewSameDirectionSpeedMultiplier;
        public bool? NewUseEntryDirection;
        public DreamTunnelSpeedConfiguration? NewSpeedConfiguration;
        public float? NewCustomSpeed;
        public bool? NewAllowDashCancels;
        public bool? NewRedirectConsumesNormalDash;
        public bool? NewAllowTransitions;
        public bool? NewBounceOnCollision;
    }
}
