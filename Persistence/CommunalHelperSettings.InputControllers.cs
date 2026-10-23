using Celeste.Everest.Input.Bindings;
using Celeste.Everest.Settings.Metadata;
using Celeste.Input.Devices.GamePads;
using Celeste.Input.Devices.Keyboard;

namespace Celeste.Mods.CommunalHelper.Persistence
{
    public sealed partial class CommunalHelperSettings
    {
        [DefaultButtonBinding(SourceGamePadButtonFlags.RightShoulder, SourceKeys.Z)]
        public EverestButtonBinding ActivateFlagController { get; set; }
    }
}
