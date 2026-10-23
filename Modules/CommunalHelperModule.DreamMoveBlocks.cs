// SPDX-License-Identifier: MIT

using System;
using Celeste.Mods.CommunalHelper.Entities.DreamBlocks;
using Celeste.Mods.CommunalHelper.Runtime.DreamBlocks;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public partial class CommunalHelperModule
    {
        private IDisposable dreamMoveBlocks;
        private void LoadDreamMoveBlocks() => dreamMoveBlocks = DreamMoveBlockProcess.Install();

        private void UnloadDreamMoveBlocks()
        {
            dreamMoveBlocks?.Dispose();
            dreamMoveBlocks = null;
        }

        private void InitializeDreamMoveParticles() => DreamMoveBlock.InitializeParticles(SourceEngine);
    }
}
