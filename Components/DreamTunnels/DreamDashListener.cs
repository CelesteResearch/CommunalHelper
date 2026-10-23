using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.CommunalHelper.Components.DreamTunnels
{
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Components.DreamDashListener")]
    public class DreamDashListener : SourceComponentState
    {
        public Action<Vector2> OnDreamDash;

        public DreamDashListener() : base(false, false)
        {
        }

        public DreamDashListener(Action<Vector2> onDreamDash) : base(false, false)
        {
            OnDreamDash = onDreamDash;
        }
    }
}
