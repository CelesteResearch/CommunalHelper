// Copyright (c) 2020 Flynn Swainston-Calcutt. Licensed under the MIT License.

using Celeste.Mods.CommunalHelper.Runtime.MoveBlocks.Redirecting;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public sealed partial class CommunalHelperModule
    {
        private MoveBlockRedirectProcess moveBlockRedirects;
        private void LoadMoveBlockRedirects() => moveBlockRedirects = MoveBlockRedirectProcess.Install(SourceEngine);

        private void UnloadMoveBlockRedirects()
        {
            moveBlockRedirects?.Dispose();
            moveBlockRedirects = null;
        }
    }
}
