using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Interop;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Game.Entities.Hazards.Spinners.Crystal;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Hazards.Spinners.Crystal;

namespace Celeste.Mods.CommunalHelper.Runtime.Hazards
{
    public sealed class TimedSpikeCapabilities : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, TimedSpikeCapabilities> Owners = new();

        private static readonly Action<SceneEntityState, object> SetScene =
            (Action<SceneEntityState, object>)typeof(SceneEntityState).GetProperty(nameof(SceneEntityState.Scene))
                .GetSetMethod(true).CreateDelegate(typeof(Action<SceneEntityState, object>));

        private readonly ISourceEngineContext engine;
        private readonly OriginalCrystalStaticSpinner spinner;
        private IDisposable imports;

        [MonoMod.ModInterop.ModImportName("GravityHelper.IsPlayerInverted")]
        public Func<bool> IsPlayerInverted;

        [MonoMod.ModInterop.ModImportName("GravityHelper.CreatePlayerGravityListener")]
        public Func<Action<RoomPlayerSession, int, float>, SourceComponentState> CreatePlayerGravityListener;

        private TimedSpikeCapabilities(ISourceEngineContext engine)
        {
            this.engine = engine;
            spinner = new OriginalCrystalStaticSpinner(Vector2.Zero, false, CrystalSpinnerColor.Rainbow, engine);
        }

        public static TimedSpikeCapabilities For(ISourceEngineContext engine) =>
            Owners.GetValue(engine, owner => new TimedSpikeCapabilities(owner));

        public static IDisposable Install(ISourceEngineContext engine)
        {
            var owner = For(engine);
            if (owner.imports == null)
            {
                var fields = new[]
                {
                    typeof(TimedSpikeCapabilities).GetField(nameof(IsPlayerInverted)),
                    typeof(TimedSpikeCapabilities).GetField(nameof(CreatePlayerGravityListener))
                };
                owner.imports = ((ISourceModInteropEnvironment)engine).ModInterop.RegisterOwned(
                    new SourceModInteropActualTypeBinding(typeof(TimedSpikeCapabilities), owner,
                        exportedMethods: Array.Empty<MethodInfo>(), importedFields: fields));
            }

            return owner;
        }

        public bool HasModule(string name)
        {
            foreach (var module in ((ISourceEverestModulesEnvironment)engine).Modules)
                if (module.Metadata.Name == name &&
                    Celeste.Everest.Modules.Dependencies.EverestDependencyVersion.Satisfies(
                        name == "MaxHelpingHand" ? new Version(1, 9, 3) : new Version(1, 0, 28),
                        module.Metadata.Version))
                    return true;
            return false;
        }

        public PackedColor GetHue(object scene, Vector2 position)
        {
            SetScene(spinner, scene);
            return spinner.GetHue(position);
        }

        public void Dispose()
        {
            imports?.Dispose();
            imports = null;
        }
    }
}
