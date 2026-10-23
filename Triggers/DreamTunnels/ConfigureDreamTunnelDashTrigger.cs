using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Simulation;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Mods.CommunalHelper.Configuration.DreamTunnels;
using Celeste.Mods.CommunalHelper.Runtime.DreamTunnels;

namespace Celeste.Mods.CommunalHelper.Triggers.DreamTunnels
{
    [CustomEntity("CommunalHelper/ConfigureDreamTunnelDashTrigger")]
    [EverestTrackedAs(
        typeof(AbstractConfigureStateTrigger<DreamTunnelDashConfiguration, DreamTunnelDashConfigurationChanges>))]
    public class ConfigureDreamTunnelDashTrigger : AbstractConfigureStateTrigger<DreamTunnelDashConfiguration,
        DreamTunnelDashConfigurationChanges>
    {
        public ConfigureDreamTunnelDashTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(engine, data, offset)
        {
        }

        protected override DreamTunnelDashConfiguration GetConfiguredOptions(EntityData data)
            => new()
            {
                AllowRedirect = data.Bool("allowRedirect", false),
                AllowSameDirectionRedirect = data.Bool("allowSameDirectionRedirect", false),
                SameDirectionSpeedMultiplier = data.Float("sameDirectionSpeedMultiplier", 1f),
                UseEntryDirection = data.Bool("useEntryDirection", false),
                SpeedConfiguration = (DreamTunnelSpeedConfiguration)data.Int("speedConfiguration", 0),
                CustomSpeed = data.Float("customSpeed", 0f),
                AllowDashCancels = data.Bool("allowDashCancels", false),
                RedirectConsumesNormalDash = data.Bool("redirectConsumesNormalDash", false),
                AllowTransitions = data.Bool("allowTransitions", false),
                BounceOnCollision = data.Bool("bounceOnCollision", false)
            };

        protected override DreamTunnelDashConfiguration GetCurrentOptions(RoomPlayerSession player)
            => Runtime.Configuration;

        protected override void SaveOptions(RoomPlayerSession player, DreamTunnelDashConfiguration options)
            => Runtime.SetConfiguration(options);

        protected override DreamTunnelDashConfigurationChanges CalculateChangesNeededToRevert(
            DreamTunnelDashConfiguration from, DreamTunnelDashConfiguration to)
            => new()
            {
                NewAllowRedirect = to.AllowRedirect == from.AllowRedirect ? null : from.AllowRedirect,
                NewAllowSameDirectionRedirect = to.AllowSameDirectionRedirect == from.AllowSameDirectionRedirect
                    ? null
                    : from.AllowSameDirectionRedirect,
                NewSameDirectionSpeedMultiplier = to.SameDirectionSpeedMultiplier == from.SameDirectionSpeedMultiplier
                    ? null
                    : from.SameDirectionSpeedMultiplier,
                NewUseEntryDirection = to.UseEntryDirection == from.UseEntryDirection ? null : from.UseEntryDirection,
                NewSpeedConfiguration =
                    to.SpeedConfiguration == from.SpeedConfiguration ? null : from.SpeedConfiguration,
                NewCustomSpeed = to.CustomSpeed == from.CustomSpeed ? null : from.CustomSpeed,
                NewAllowDashCancels = to.AllowDashCancels == from.AllowDashCancels ? null : from.AllowDashCancels,
                NewRedirectConsumesNormalDash = to.RedirectConsumesNormalDash == from.RedirectConsumesNormalDash
                    ? null
                    : from.RedirectConsumesNormalDash,
                NewAllowTransitions = to.AllowTransitions == from.AllowTransitions ? null : from.AllowTransitions,
                NewBounceOnCollision = to.BounceOnCollision == from.BounceOnCollision ? null : from.BounceOnCollision
            };

        protected override DreamTunnelDashConfiguration RevertChanges(DreamTunnelDashConfiguration current,
            DreamTunnelDashConfigurationChanges? changesNeededToRevert)
            => new()
            {
                AllowRedirect = changesNeededToRevert?.NewAllowRedirect ?? current.AllowRedirect,
                AllowSameDirectionRedirect = changesNeededToRevert?.NewAllowSameDirectionRedirect ??
                                             current.AllowSameDirectionRedirect,
                SameDirectionSpeedMultiplier = changesNeededToRevert?.NewSameDirectionSpeedMultiplier ??
                                               current.SameDirectionSpeedMultiplier,
                UseEntryDirection = changesNeededToRevert?.NewUseEntryDirection ?? current.UseEntryDirection,
                SpeedConfiguration = changesNeededToRevert?.NewSpeedConfiguration ?? current.SpeedConfiguration,
                CustomSpeed = changesNeededToRevert?.NewCustomSpeed ?? current.CustomSpeed,
                AllowDashCancels = changesNeededToRevert?.NewAllowDashCancels ?? current.AllowDashCancels,
                RedirectConsumesNormalDash = changesNeededToRevert?.NewRedirectConsumesNormalDash ??
                                             current.RedirectConsumesNormalDash,
                AllowTransitions = changesNeededToRevert?.NewAllowTransitions ?? current.AllowTransitions,
                BounceOnCollision = changesNeededToRevert?.NewBounceOnCollision ?? current.BounceOnCollision
            };
    }
}
