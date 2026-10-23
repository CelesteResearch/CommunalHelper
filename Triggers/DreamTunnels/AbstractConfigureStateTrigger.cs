using System;
using Celeste.Core.Components;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Simulation;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Simulation.Gameplay.Triggers.Foundation;
using Celeste.Mods.CommunalHelper.Components.DreamTunnels;
using Celeste.Mods.CommunalHelper.Runtime.DreamTunnels;
using System.Linq;

namespace Celeste.Mods.CommunalHelper.Triggers.DreamTunnels
{
// unfortunately, due to this class being generic, `[SceneTracked(true)]` doesn't really do anything here
// all children of this class need to be marked as `[TrackedAs(typeof(AbstractConfigureStateTrigger<TOptions, TChanges>))]` in order to be tracked properly
    [SceneTracked(true)]
    public abstract class AbstractConfigureStateTrigger<TOptions, TChanges> : SourceTriggerEntityState
        where TOptions : struct where TChanges : struct
    {
        private readonly TOptions options;
        private TChanges? changesNeededToRevert;

        private readonly bool revertOnLeave;
        private readonly bool revertOnDeath;
        private readonly bool onlyOnce;

        protected DreamTunnelDashRuntime Runtime { get; }

        public AbstractConfigureStateTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            Runtime = DreamTunnelDashRuntime.For(engine);
            revertOnLeave = data.Bool("revertOnLeave", false);
            revertOnDeath = data.Bool("revertOnDeath", true);
            onlyOnce = data.Bool("onlyOnce", false);

            options = GetConfiguredOptions(data);

            string flag = data.Attr("flag");
            if (!string.IsNullOrEmpty(flag))
            {
                Components.Add(new FlagToggleComponent(flag, data.Bool("flagInverted")));
            }
        }

        protected abstract TOptions GetConfiguredOptions(EntityData data);
        protected abstract TOptions GetCurrentOptions(RoomPlayerSession player);
        protected abstract void SaveOptions(RoomPlayerSession player, TOptions options);

        protected abstract TChanges CalculateChangesNeededToRevert(TOptions from, TOptions to);
        protected abstract TOptions RevertChanges(TOptions current, TChanges? changesNeededToRevert);

        public override void OnEnter(RoomPlayerSession player)
        {
            changesNeededToRevert = CalculateChangesNeededToRevert(GetCurrentOptions(player), options);
            SaveOptions(player, options);

            if (onlyOnce)
            {
                RemoveSelf();
            }
        }

        public override void OnLeave(RoomPlayerSession player)
        {
            if (revertOnLeave && !player.Dead)
            {
                SaveOptions(player, RevertChanges(options, changesNeededToRevert));
            }
        }

        internal static void RestoreOnDeath(RoomPlayerSession player)
        {
            foreach (AbstractConfigureStateTrigger<TOptions, TChanges> trigger in ((ISourceScene)player.Scene)
                     .SourceScene.Entities
                     .GetTrackedEntities(typeof(AbstractConfigureStateTrigger<TOptions, TChanges>)))
            {
                if (trigger.revertOnDeath && trigger.changesNeededToRevert is not null)
                    trigger.SaveOptions(player, trigger.RevertChanges(trigger.options, trigger.changesNeededToRevert));
            }
        }
    }
}
