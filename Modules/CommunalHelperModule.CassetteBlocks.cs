using System;
using Celeste.Mods.CommunalHelper.Runtime.CassetteBlocks;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public sealed partial class CommunalHelperModule
    {
        private IDisposable customCassetteBlocks;
        private void LoadCassetteBlocks() => customCassetteBlocks = CustomCassetteBlockProcess.Install(SourceEngine);
        private void InitializeCassetteBlocks() => CustomCassetteBlockProcess.Initialize(SourceEngine);

        private void UnloadCassetteBlocks()
        {
            customCassetteBlocks?.Dispose();
            customCassetteBlocks = null;
        }
    }
}
