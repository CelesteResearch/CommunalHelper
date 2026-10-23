using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.CommunalHelper.Entities.Audio;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Contacts;
using Celeste.Simulation.Gameplay.Player.Scene;
using Celeste.Simulation.Gameplay.Player.Dash;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.Audio
{
    internal sealed class UnderwaterMusicHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly string registryKey;
        private readonly Hook underwater;
        private readonly ILHook dash;

        public UnderwaterMusicHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            registryKey = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog.Find(typeof(UnderwaterMusicController))
                .RegistryKey;
            underwater = new Hook(typeof(PlayerContacts).GetMethod(nameof(PlayerContacts.UnderwaterMusicCheck)),
                (Func<Func<PlayerContacts, bool>, PlayerContacts, bool>)CheckUnderwater);
            try
            {
                dash = new ILHook(
                    typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.Emit), new[] { typeof(DashEffect) }),
                    PatchDash);
            }
            catch
            {
                underwater.Dispose();
                throw;
            }
        }

        private UnderwaterMusicController First(object scene)
        {
            if (scene is OriginalLevelSceneRoot level)
            {
                var items = ((ISourceScene)level).SourceScene.Entities.GetTrackedEntities(registryKey);
                if (items.Count > 0)
                    return (UnderwaterMusicController)items[0];
            }

            return null;
        }

        private bool CheckUnderwater(Func<PlayerContacts, bool> orig, PlayerContacts self)
        {
            var player = (RoomPlayerSession)self.ActualOwner;
            if (!ReferenceEquals(player.ActorEngineContext, engine)) return orig(self);
            var controller = First(player.Scene);
            return controller != null ? controller.CheckEnabled() : orig(self);
        }

        private void PatchDash(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    i => i.MatchCall<RoomPlayerWorld>(nameof(RoomPlayerWorld.SwimCheck))))
                return;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, RoomPlayerWorld, bool>>(DashSwimCheck);
        }

        private bool DashSwimCheck(bool orig, RoomPlayerWorld self)
        {
            if (!ReferenceEquals(self.EngineContext, engine)) return orig;
            var controller = First(engine.Frame.Scenes.CurrentScene);
            return controller == null || !controller.DashSFX ? orig : controller.CheckEnabled();
        }

        public void Dispose()
        {
            dash.Dispose();
            underwater.Dispose();
        }
    }
}
