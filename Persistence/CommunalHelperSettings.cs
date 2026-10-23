using System.Collections.Generic;
using Celeste.Core.Reflection.Runtime;
using Celeste.Game.Content.Dialogs;
using Celeste.Everest.UI.Settings;
using Celeste.Simulation.UI.Menus.Text;
using Celeste.Everest.Modules.Settings;
using Celeste.Everest.Settings.Metadata;

namespace Celeste.Mods.CommunalHelper.Persistence
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.CommunalHelperSettings")]
    public sealed partial class CommunalHelperSettings : EverestModuleSettings
    {
        private bool dreamDashFeatherMode;
        private bool dreamTunnelIgnoreCollidables;
        internal bool SavingSettings;

        public bool AlwaysActiveDreamRefillCharge { get; set; }

        [SettingIgnore]
        public bool DreamDashFeatherMode
        {
            get => (SavingSettings || AlwaysActiveDreamRefillCharge) && dreamDashFeatherMode;
            set => dreamDashFeatherMode = value;
        }

        [SettingIgnore]
        public bool DreamTunnelIgnoreCollidables
        {
            get => (SavingSettings || AlwaysActiveDreamRefillCharge) && dreamTunnelIgnoreCollidables;
            set => dreamTunnelIgnoreCollidables = value;
        }

        public void CreateAlwaysActiveDreamRefillChargeEntry(TextMenuPresentation menu, bool inGame)
        {
            var dialog = ((IOriginalGameTextEnvironment)menu.EngineContext).Text.Dialog;
            var featherModeOption = new TextMenuPresentation.OnOff(dialog.Clean("SETTINGS_DREAMTUNNEL_FEATHERMODE"),
                dreamDashFeatherMode, menu.EngineContext).Change(value => dreamDashFeatherMode = value);
            var ignoreCollectiblesOption = new TextMenuPresentation.OnOff(
                dialog.Clean("SETTINGS_DREAMTUNNEL_IGNORECOLLECTIBLES"),
                dreamTunnelIgnoreCollidables, menu.EngineContext).Change(value => dreamTunnelIgnoreCollidables = value);
            menu.Add(new EverestOptionSubMenu(menu.Resources, dialog.Clean("SETTINGS_DREAMTUNNEL_ALWAYSACTIVE"))
                .Add(dialog.Clean("OPTIONS_OFF"), null)
                .Add(dialog.Clean("OPTIONS_ON"),
                    new List<TextMenuPresentation.Item> { featherModeOption, ignoreCollectiblesOption })
                .Change(value => AlwaysActiveDreamRefillCharge = value == 1)
                .SetInitialSelection(AlwaysActiveDreamRefillCharge ? 1 : 0));
        }
    }
}
