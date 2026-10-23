using System;
using System.Runtime.CompilerServices;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Mods.CommunalHelper.Configuration.DreamTunnels;
using Celeste.Mods.CommunalHelper.Persistence;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamTunnels
{
    public sealed partial class DreamTunnelDashRuntime
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, DreamTunnelDashRuntime> contexts = new();
        private Func<CommunalHelperSettings> settings;
        private Func<CommunalHelperSession> session;
        private int chargeCount;
        private bool nextDashFeather;
        private bool canStartAttack;
        private bool attacking;
        private float attackTimer;
        private bool overrideDreamDashCheck;
        private int trailColorIndex;
        private PackedColor[] trailColors;

        private DreamTunnelDashRuntime(ISourceEngineContext engine)
        {
            Engine = engine;
        }

        public static DreamTunnelDashRuntime For(ISourceEngineContext engine)
            => contexts.GetValue(engine ?? throw new ArgumentNullException(nameof(engine)),
                owner => new DreamTunnelDashRuntime(owner));

        public ISourceEngineContext Engine { get; }

        public CommunalHelperSettings Settings => (settings ?? throw new InvalidOperationException(
            "Dream tunnel module settings are not bound."))();

        public DreamTunnelDashConfiguration Configuration => (session ?? throw new InvalidOperationException(
            "Dream tunnel module session is not bound."))().CurrentDreamTunnelDashConfiguration;

        public void SetConfiguration(DreamTunnelDashConfiguration configuration)
            => (session ?? throw new InvalidOperationException("Dream tunnel module session is not bound."))()
                .CurrentDreamTunnelDashConfiguration = configuration;

        public int ChargeCount
        {
            get => Settings.AlwaysActiveDreamRefillCharge ? 1 : chargeCount;
            set => chargeCount = value;
        }

        public bool NextDashFeather
        {
            get => nextDashFeather || Settings.DreamDashFeatherMode;
            set => nextDashFeather = value;
        }

        public bool FeatherMode { get; private set; }
        public bool Attacking => attacking;

        public void Bind(Func<CommunalHelperSettings> actualSettings, Func<CommunalHelperSession> actualSession)
        {
            settings = actualSettings ?? throw new ArgumentNullException(nameof(actualSettings));
            session = actualSession ?? throw new ArgumentNullException(nameof(actualSession));
        }

        public void InitializeParticles()
        {
            trailColors = new[]
            {
                new PackedColor(255, 239, 17), new PackedColor(8, 163, 16), new PackedColor(255, 0, 208),
                new PackedColor(95, 205, 228), new PackedColor(224, 86, 76)
            };
        }

        private void UseCharge()
        {
            if (ChargeCount > 0)
            {
                canStartAttack = true;
                if (NextDashFeather)
                {
                    FeatherMode = true;
                    NextDashFeather = false;
                }

                ChargeCount--;
            }
            else
                canStartAttack = false;
        }
    }
}
