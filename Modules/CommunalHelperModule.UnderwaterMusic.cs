using System;
using Celeste.Mods.CommunalHelper.Runtime.Audio;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public sealed partial class CommunalHelperModule
    {
        private IDisposable underwaterMusic;
        private void LoadUnderwaterMusic() => underwaterMusic = new UnderwaterMusicHooks(SourceEngine);

        private void UnloadUnderwaterMusic()
        {
            underwaterMusic?.Dispose();
            underwaterMusic = null;
        }
    }
}
