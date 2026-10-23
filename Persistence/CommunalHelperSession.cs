using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Persistence;
using Celeste.Mods.CommunalHelper.Entities.Stations;

namespace Celeste.Mods.CommunalHelper.Persistence
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.CommunalHelperSession")]
    public partial class CommunalHelperSession : EverestModuleSession
    {
        public StationBlockTrack.TrackSwitchState TrackInitialState = StationBlockTrack.TrackSwitchState.On;
        public StationBlockTrack.TrackMoveMode TrackInitialMoveMode = StationBlockTrack.TrackMoveMode.ForwardForce;
    }
}
