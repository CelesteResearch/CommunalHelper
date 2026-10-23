using Celeste.Core.Scheduling;
using Celeste.Game.Entities.Platforms.SwapBlocks.Motion;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    public partial class CassetteSwapBlock
    {
        private readonly struct ProgressState(CassetteSwapBlock block) : ISwapBlockProgressState
        {
            public ISourceEngineContext Engine => block.Engine;
            public float MaxForwardSpeed => block.maxForwardSpeed;
            public float MaxBackwardSpeed => block.maxBackwardSpeed;
            public ref int Target => ref block.target;
            public ref float Lerp => ref block.lerp;
            public ref float Speed => ref block.speed;
        }
    }
}
