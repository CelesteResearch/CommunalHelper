using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.CommunalHelper.Entities.DreamTunnels
{
    [SceneTracked]
    [CustomEntity("CommunalHelper/DreamTunnelBlocker")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.DreamTunnelBlocker")]
    public class DreamTunnelBlocker : SceneEntityState
    {
        public bool BlockDreamTunnelDashes;
        public bool BlockDreamDashes;

        public DreamTunnelBlocker(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            Collider = new HitboxColliderGeometry(data.Width, data.Height);
            Depth = -11000;
            BlockDreamTunnelDashes = data.Bool("blockDreamTunnelDashes", true);
            BlockDreamDashes = data.Bool("blockDreamDashes", false);
        }
    }
}
