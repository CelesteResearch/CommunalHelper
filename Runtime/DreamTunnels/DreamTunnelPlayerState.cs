using Celeste.Physics.Collision;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamTunnels
{
    internal sealed class DreamTunnelPlayerState
    {
        public int Index = -1;
        public float CanEndTimer;
        public SourceSolidEntityState Solid;
        public SourceCollisionHandler OnCollideH;
        public SourceCollisionHandler OnCollideV;
    }
}
