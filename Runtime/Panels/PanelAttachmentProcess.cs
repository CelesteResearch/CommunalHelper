// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Walls.Breakable;
using Celeste.Mods.CommunalHelper.Entities.Panels;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.Panels
{
    internal sealed class PanelAttachmentProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();

        internal PanelAttachmentProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            try
            {
                hooks.Add(new Hook(typeof(SourceSolidEntityState).GetMethod(nameof(SourceSolidEntityState.Awake)),
                    (Action<Action<SourceSolidEntityState, object>, SourceSolidEntityState, object>)Awake));
                hooks.Add(new Hook(typeof(OriginalDashBlockState).GetMethod(nameof(OriginalDashBlockState.Break)),
                    (Action<Action<OriginalDashBlockState, Vector2, Vector2, bool, bool>, OriginalDashBlockState,
                        Vector2, Vector2, bool, bool>)Break));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(object scene) =>
            scene is ISourceEngineContextScene owner && ReferenceEquals(owner.EngineContext, engine);

        private void Awake(Action<SourceSolidEntityState, object> original, SourceSolidEntityState solid, object scene)
        {
            original(solid, scene);
            if (Owns(scene) && !solid.AllowStaticMovers) AbstractPanel.ForceAttachStaticMovers(solid, scene);
        }

        private void Break(Action<OriginalDashBlockState, Vector2, Vector2, bool, bool> original,
            OriginalDashBlockState block, Vector2 from, Vector2 direction, bool playSound, bool playDebrisSound)
        {
            original(block, from, direction, playSound, playDebrisSound);
            if (!Owns(block.Scene)) return;
            var movers = block.StaticMovers;
            for (int i = 0; i < movers.Count; i++)
                if (movers[i].Entity is AbstractPanel)
                    movers[i].OnDestroy();
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
