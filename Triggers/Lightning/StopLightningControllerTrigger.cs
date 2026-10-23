using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;
using Celeste.Mods.CommunalHelper.Entities.Lightning;

namespace Celeste.Mods.CommunalHelper.Triggers.Lightning
{
    [CustomEntity("CommunalHelper/StopLightningControllerTrigger")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Triggers.StopLightningControllerTrigger")]
    public sealed class StopLightningControllerTrigger : SourceTriggerEntityState
    {
        public StopLightningControllerTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            foreach (LightningController item in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(LightningController)))
                item.RemoveSelf();
            Collidable = Active = false;
        }
    }
}
