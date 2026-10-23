using System;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Simulation;

namespace Celeste.Mods.CommunalHelper.Components.DreamTunnels
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Components.DreamTunnelInteraction")]
    public class DreamTunnelInteraction : SourceComponentState
    {
        public Action<RoomPlayerSession> OnPlayerEnter;
        public Action<RoomPlayerSession> OnPlayerExit;

        public DreamTunnelInteraction(Action<RoomPlayerSession> onPlayerEnter, Action<RoomPlayerSession> onPlayerExit)
            : base(false, false)
        {
            OnPlayerEnter = onPlayerEnter;
            OnPlayerExit = onPlayerExit;
        }
    }
}
