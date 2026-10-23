using System;
using Celeste.Core.Components;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Entities;

namespace Celeste.Mods.CommunalHelper.Components.DreamTunnels
{
    public sealed class FlagToggleComponent : SourceComponentState
    {
        public bool Enabled = true;
        private readonly string flag;
        private readonly bool inverted;
        private readonly Action onDisable;
        private readonly Action onEnable;

        public FlagToggleComponent(string flag, bool inverted, Action onDisable = null, Action onEnable = null)
            : base(true, false)
        {
            this.flag = flag;
            this.inverted = inverted;
            this.onDisable = onDisable;
            this.onEnable = onEnable;
        }

        public override void EntityAdded(object scene)
        {
            base.EntityAdded(scene);
            UpdateFlag();
        }

        public override void Update()
        {
            base.Update();
            UpdateFlag();
        }

        public void UpdateFlag()
        {
            if ((inverted || ((OriginalLevelSceneRoot)Scene).Session.GetFlag(flag) == Enabled)
                && (!inverted || ((OriginalLevelSceneRoot)Scene).Session.GetFlag(flag) != Enabled)) return;

            {
                var entity = (SceneEntityState)Entity;
                if (Enabled)
                {
                    entity.Visible = entity.Collidable = false;
                    onDisable?.Invoke();
                    Enabled = false;
                }
                else
                {
                    entity.Visible = entity.Collidable = true;
                    onEnable?.Invoke();
                    Enabled = true;
                }
            }
        }
    }
}
