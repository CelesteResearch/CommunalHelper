// SPDX-License-Identifier: MIT

using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.CommunalHelper.Runtime.Panels
{
    [Celeste.Everest.Tracking.Attributes.EverestTrackedAs(typeof(SourceStaticMoverState))]
    public sealed class PanelStaticMover : SourceStaticMoverState
    {
        internal void AttachForced(SourceSolidEntityState solid) => AttachToPlatform(solid);
    }
}
