using System;
using System.Reflection;
using Celeste.Core.Reflection.Interop;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using MonoMod.ModInterop;

namespace Celeste.Mods.CommunalHelper.Runtime.DreamTunnels
{
    public sealed partial class DreamTunnelDashRuntime
    {
        [ModImportName("GravityHelper.IsActorInverted")]
        public Func<SourceActorEntityState, bool> IsActorInverted;

        private IDisposable gravityImports;

        private void LoadGravityImports()
        {
            gravityImports = ((ISourceModInteropEnvironment)Engine).ModInterop.RegisterOwned(
                new SourceModInteropActualTypeBinding(typeof(DreamTunnelDashRuntime), this,
                    exportedMethods: Array.Empty<MethodInfo>(),
                    importedFields: new[] { typeof(DreamTunnelDashRuntime).GetField(nameof(IsActorInverted)) }));
        }

        private bool IsInverted(RoomPlayerSession player) => IsActorInverted?.Invoke(player) ?? false;
    }
}
