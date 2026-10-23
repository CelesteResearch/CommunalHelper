namespace Celeste.Mods.CommunalHelper.Configuration.DreamTunnels
{
    public struct DreamTunnelDashConfiguration
    {
        public bool AllowRedirect;
        public bool AllowSameDirectionRedirect;
        public float SameDirectionSpeedMultiplier;
        public bool UseEntryDirection;
        public DreamTunnelSpeedConfiguration SpeedConfiguration;
        public float CustomSpeed;
        public bool AllowDashCancels;
        public bool RedirectConsumesNormalDash;
        public bool AllowTransitions;
        public bool BounceOnCollision;

        public static readonly DreamTunnelDashConfiguration Default = new DreamTunnelDashConfiguration
        {
            AllowRedirect = false,
            AllowSameDirectionRedirect = false,
            SameDirectionSpeedMultiplier = 1f,
            UseEntryDirection = false,
            SpeedConfiguration = DreamTunnelSpeedConfiguration.Default,
            CustomSpeed = 0f,
            AllowDashCancels = false,
            RedirectConsumesNormalDash = false,
            AllowTransitions = false,
            BounceOnCollision = false
        };
    }
}
