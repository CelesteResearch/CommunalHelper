// SPDX-License-Identifier: MIT

using System;
using Celeste.Mods.CommunalHelper.Runtime.WallBoosters;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public partial class CommunalHelperModule
    {
        private IDisposable attachedWallBoosters;
        private void LoadAttachedWallBoosters() => attachedWallBoosters = new AttachedWallBoosterProcess(SourceEngine);

        private void UnloadAttachedWallBoosters()
        {
            attachedWallBoosters?.Dispose();
            attachedWallBoosters = null;
        }
    }
}
