// SPDX-License-Identifier: MIT

using Celeste.Mods.CommunalHelper.Runtime.Panels;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public partial class CommunalHelperModule
    {
        private PanelAttachmentProcess panels;
        private void LoadPanels() => panels ??= new PanelAttachmentProcess(SourceEngine);

        private void UnloadPanels()
        {
            panels?.Dispose();
            panels = null;
        }
    }
}
