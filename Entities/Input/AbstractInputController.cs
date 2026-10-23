using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Core.Components;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.CommunalHelper.Entities.Input
{
    [SceneTracked(true)]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.AbstractInputController")]
    public abstract class AbstractInputController : SceneEntityState
    {
        protected AbstractInputController(ISourceEngineContext engine) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
        }

        protected AbstractInputController(ISourceEngineContext engine, Vector2 position) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
        }

        public abstract void FrozenUpdate();
    }
}
