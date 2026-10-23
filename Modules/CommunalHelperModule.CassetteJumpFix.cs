using System;
using Celeste.Mods.CommunalHelper.Runtime.CassetteBlocks;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public sealed partial class CommunalHelperModule
    {
        private IDisposable cassetteJumpFix;

        private void LoadCassetteJumpFix() =>
            cassetteJumpFix = CassetteJumpFixProcess.Install(SourceEngine, () => Session);

        private void UnloadCassetteJumpFix()
        {
            cassetteJumpFix?.Dispose();
            cassetteJumpFix = null;
        }
    }
}
