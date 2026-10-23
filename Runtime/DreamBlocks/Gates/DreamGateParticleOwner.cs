using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Processes;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamBlocks.Gates
{
    internal sealed class DreamGateParticleOwner
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, DreamGateParticleOwner> owners = new();
        public readonly ParticleTypePresentation[] Types = new ParticleTypePresentation[4];

        private DreamGateParticleOwner(ISourceEngineContext engine)
        {
            var template =
                new ParticleTypePresentation(((IOriginalGameplayProcess)engine).ParticleTypes.SwitchGate.P_Behind)
                {
                    ColorMode = ParticleTypePresentation.ColorModes.Blink
                };
            for (int i = 0; i < 4; i++) Types[i] = new ParticleTypePresentation(template);
        }

        public static DreamGateParticleOwner For(ISourceEngineContext engine) =>
            owners.GetValue(engine, static owner => new DreamGateParticleOwner(owner));
    }
}
