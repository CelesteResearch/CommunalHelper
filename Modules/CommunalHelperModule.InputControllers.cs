using Celeste.Mods.CommunalHelper.Runtime.Input;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public sealed partial class CommunalHelperModule
    {
        private FrozenInputControllerHooks inputControllers;
        private void LoadInputControllers() => inputControllers = new FrozenInputControllerHooks(SourceEngine);

        private void UnloadInputControllers()
        {
            inputControllers?.Dispose();
            inputControllers = null;
        }
    }
}
