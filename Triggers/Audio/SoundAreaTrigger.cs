using System.Numerics;
using System.Reflection;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.CommunalHelper.Triggers.Audio
{
    [CustomEntity("CommunalHelper/SoundAreaTrigger")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Triggers.SoundAreaTrigger")]
    public sealed class SoundAreaTrigger : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly ISourceAudioEventReference eventInstance;
        private float vol;

        public SoundAreaTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            Vector2 node = data.NodesOffset(offset)[0];
            var sound = new SoundSourceComponentState(engine, node - Position, data.Attr("event", ""));
            eventInstance = (ISourceAudioEventReference)typeof(SoundSourceComponentState)
                .GetField("instance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sound);
            OwnedComponents.Add(sound);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            eventInstance?.SetVolume(vol);
        }

        public override void Update()
        {
            base.Update();
            vol = Celeste.Core.Mathematics.ScalarMath.Approach(vol, PlayerIsInside ? 1f : 0f, engine.DeltaTime * 2f);
            eventInstance?.SetVolume(vol);
        }
    }
}
