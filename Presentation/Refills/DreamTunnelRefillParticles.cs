using System;
using System.Runtime.CompilerServices;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.CommunalHelper.Presentation.Refills
{
    public sealed class DreamTunnelRefillParticles
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, DreamTunnelRefillParticles> contexts = new();
        private readonly ISourceEngineContext engine;

        private DreamTunnelRefillParticles(ISourceEngineContext engine)
        {
            this.engine = engine;
        }

        public static DreamTunnelRefillParticles For(ISourceEngineContext engine)
            => contexts.GetValue(engine ?? throw new ArgumentNullException(nameof(engine)),
                owner => new DreamTunnelRefillParticles(owner));

        public ParticleTypePresentation[] Glow { get; private set; }
        public ParticleTypePresentation[] Shatter { get; private set; }
        public ParticleTypePresentation[] Regen { get; private set; }

        public void InitializeParticles()
        {
            var source = ((IOriginalRefillProcess)engine).RefillParticles;
            Shatter = Create(source.P_Shatter);
            Regen = Create(source.P_Regen);
            Glow = Create(source.P_Glow);
        }

        private static ParticleTypePresentation[] Create(ParticleTypePresentation source)
        {
            var particle = new ParticleTypePresentation(source)
                { ColorMode = ParticleTypePresentation.ColorModes.Choose };
            return new[]
            {
                new ParticleTypePresentation(particle)
                    { Color = new PackedColor(255, 239, 17), Color2 = new PackedColor(255, 0, 208) },
                new ParticleTypePresentation(particle)
                    { Color = new PackedColor(8, 163, 16), Color2 = new PackedColor(95, 205, 228) },
                new ParticleTypePresentation(particle)
                    { Color = new PackedColor(127, 178, 94), Color2 = new PackedColor(224, 86, 76) },
                new ParticleTypePresentation(particle)
                    { Color = new PackedColor(91, 110, 225), Color2 = new PackedColor(204, 59, 59) }
            };
        }
    }
}
