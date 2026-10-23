// SPDX-License-Identifier: MIT

using System;
using Celeste.Graphics.Content;
using Celeste.Mods.CommunalHelper.Entities.Effects;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public partial class CommunalHelperModule
    {
        private IDisposable cassetteMoveDebrisPool;

        private void LoadCassetteMoveDebris()
        {
            cassetteMoveDebrisPool ??= SourceEngine.EntityPools.RegisterScoped(() =>
                new MoveBlockDebris(((ISourceGameGraphicsEnvironment)SourceEngine).GameGraphics.GameAtlas, SourceEngine,
                    ((ISourcePlatformTargetMotionEnvironment)SourceEngine).TargetMotionPolicy));
        }

        private void UnloadCassetteMoveDebris()
        {
            cassetteMoveDebrisPool?.Dispose();
            cassetteMoveDebrisPool = null;
        }
    }
}
