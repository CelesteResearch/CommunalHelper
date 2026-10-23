// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Environment.Core;

namespace Celeste.Mods.CommunalHelper.Entities.Audio
{
    [CustomEntity("CommunalHelper/CoreModeMusicController = Create")]
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CoreModeMusicController")]
    public class CoreModeMusicController : SceneEntityState
    {
        private readonly IOriginalGameplayProcess process;
        private readonly string[] parameters;
        private readonly float hot, cold, none;
        private readonly bool disable;
        private RoomCoreMode? previousCoreMode;

        public static CoreModeMusicController Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public CoreModeMusicController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            process = (IOriginalGameplayProcess)engine;
            parameters = data.Attr("params").Split(',');
            hot = data.Float("hot", 1f);
            cold = data.Float("cold", 0f);
            none = data.Float("none", 0f);
            if (!(disable = data.Bool("disable")))
            {
                var tags = ((IOriginalGameTagsEnvironment)engine).Tags;
                AddTag(tags.TransitionUpdate.Value | tags.Persistent.Value);
            }

            Visible = false;
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (!disable) return;
            var controllers =
                ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(typeof(CoreModeMusicController));
            for (int index = 0; index < controllers.Count; index++)
            {
                var controller = (CoreModeMusicController)controllers[index];
                if (!controller.disable && controller.parameters.SequenceEqual(parameters)) controller.RemoveSelf();
            }

            RemoveSelf();
        }

        public override void Update()
        {
            base.Update();
            var current = ((OriginalLevelSceneRoot)Scene).CoreMode;
            if (previousCoreMode != current)
            {
                float value = current == RoomCoreMode.Hot ? hot : current == RoomCoreMode.Cold ? cold : none;
                foreach (string parameter in parameters) process.AudioSession.SetMusicParam(parameter, value);
                previousCoreMode = current;
            }
        }
    }
}
