using System;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Engine.Update;
using Celeste.Mods.CommunalHelper.Entities.Input;
using Celeste.Scene.Tracking;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.Input
{
    public sealed class FrozenInputControllerHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly string registryKey;
        private readonly Hook hook;

        public FrozenInputControllerHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            registryKey = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog.Find(typeof(AbstractInputController))
                .RegistryKey;
            var map = engine.GetType().GetInterfaceMap(typeof(IOriginalEngineUpdateHooks));
            MethodInfo target = null;
            for (int i = 0; i < map.InterfaceMethods.Length; i++)
                if (map.InterfaceMethods[i].Name == nameof(IOriginalEngineUpdateHooks.BeforeFreezeTimerDecrement))
                {
                    target = map.TargetMethods[i];
                    break;
                }

            hook = new Hook(target,
                (Action<Action<ISourceEngineContext, ISourceSceneExecution>, ISourceEngineContext,
                    ISourceSceneExecution>)BeforeFreezeDecrement);
        }

        private void BeforeFreezeDecrement(Action<ISourceEngineContext, ISourceSceneExecution> original,
            ISourceEngineContext self, ISourceSceneExecution scene)
        {
            if (ReferenceEquals(self, engine))
            {
                var scheduler = ((ISourceScene)scene).SourceScene;
                foreach (AbstractInputController entity in scheduler.Entities.GetTrackedEntities(registryKey))
                    entity.FrozenUpdate();
            }

            original(self, scene);
        }

        public void Dispose() => hook.Dispose();
    }
}
