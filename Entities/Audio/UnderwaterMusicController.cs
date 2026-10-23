using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.CommunalHelper.Entities.Audio
{
    [CustomEntity("CommunalHelper/UnderwaterMusicController")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.UnderwaterMusicController")]
    [SceneTracked(false)]
    public sealed class UnderwaterMusicController : SceneEntityState
    {
        public bool Enable;
        public bool DashSFX;
        public string Flag;

        public UnderwaterMusicController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            Enable = data.Bool("enable", false);
            DashSFX = data.Bool("dashSFX", false);
            Flag = data.Attr("flag", "");
        }

        public bool CheckEnabled() => Enable &&
                                      (string.IsNullOrWhiteSpace(Flag) ||
                                       ((OriginalLevelSceneRoot)Scene).Session.GetFlag(Flag));
    }
}
