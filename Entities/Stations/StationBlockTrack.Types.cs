using Celeste.Core.Reflection.Runtime;

namespace Celeste.Mods.CommunalHelper.Entities.Stations
{
    public partial class StationBlockTrack
    {
        [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.StationBlockTrack+TrackSwitchState")]
        public enum TrackSwitchState
        {
            None,
            On,
            Off
        }

        [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.StationBlockTrack+TrackMoveMode")]
        public enum TrackMoveMode
        {
            None,
            ForwardOneWay,
            BackwardOneWay,
            ForwardForce,
            BackwardForce,
        }
    }
}
