using System;
using Celeste.Core.Components;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Mods.CommunalHelper.Runtime.CassetteBlocks;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    [SceneTracked]
    [CustomEntity("CommunalHelper/CassetteJumpFixController")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CassetteJumpFixController")]
    public class CassetteJumpFixController : SceneEntityState
    {
        private readonly bool enable, persistent;
        private readonly ISourceEngineContext engine;

        public CassetteJumpFixController(EntityData data, Vector2 _, ISourceEngineContext engine)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            Visible = Collidable = false;
            enable = !data.Bool("off", false);
            persistent = data.Bool("persistent", false);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            if (persistent || !enable) CassetteJumpFixProcess.For(engine).Session.CassetteJumpFix = enable;
        }

        public static bool MustApply(object scene)
        {
            int debug = 0;
            try
            {
                var process = CassetteJumpFixProcess.For(((ISourceEngineContextScene)scene).EngineContext);
                foreach (CassetteJumpFixController controller in ((ISourceScene)scene).SourceScene.Entities
                         .GetTrackedEntities(process.ControllerKey))
                {
                    debug = 1;
                    if (controller.enable && !controller.persistent) debug = 2;
                    return true;
                }

                debug = 3;
                return process.Session.CassetteJumpFix;
            }
            catch (NullReferenceException error)
            {
                throw new NullReferenceException($"CommunalHelper: Encountered NRE at {debug} in {nameof(MustApply)}",
                    error);
            }
        }
    }
}
