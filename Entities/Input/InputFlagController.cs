using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.CommunalHelper.Modules;

namespace Celeste.Mods.CommunalHelper.Entities.Input
{
    [CustomEntity("CommunalHelper/InputFlagController")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.InputFlagController")]
    public class InputFlagController : AbstractInputController
    {
        public string[][] Flags;
        private int flagIndex;
        public bool Toggle;
        public bool ResetFlags;
        public float Delay;
        private float cooldown;
        public bool OverrideBinding;
        private readonly ISourceEngineContext engine;
        private readonly CommunalHelperModule module;

        public bool Activated => OverrideBinding
            ? ((IOriginalGameInputProcessEnvironment)engine).InputSource.Grab.Pressed
            : module.Settings.ActivateFlagController.Pressed;

        public InputFlagController(ISourceEngineContext engine, EntityData data, System.Numerics.Vector2 offset) :
            base(engine)
        {
            this.engine = engine;
            var groups = data.Attr("flags", "").Split(';');
            Flags = new string[groups.Length][];
            for (int i = 0; i < groups.Length; i++) Flags[i] = groups[i].Split(',');
            Toggle = data.Bool("toggle", true);
            ResetFlags = data.Bool("resetFlags", false);
            Delay = data.Float("delay", 0f);
            OverrideBinding = data.Bool("grabOverride", false);
            foreach (var candidate in ((ISourceEverestModulesEnvironment)engine).Modules)
                if (candidate is CommunalHelperModule owner)
                {
                    module = owner;
                    break;
                }
        }

        public override void Update()
        {
            base.Update();
            if (cooldown > 0f) cooldown -= engine.DeltaTime;
            else if (Activated) Activate();
        }

        public override void FrozenUpdate()
        {
            if (cooldown <= 0f && Activated) Activate();
        }

        public void Activate()
        {
            if (flagIndex < Flags.Length)
            {
                var group = Flags[flagIndex];
                bool value = true;
                foreach (string flag in group)
                {
                    if (Toggle) value = !((OriginalLevelSceneRoot)Scene).Session.GetFlag(flag);
                    ((OriginalLevelSceneRoot)Scene).Session.SetFlag(flag, value);
                }

                flagIndex++;
                if (Toggle && flagIndex >= Flags.Length) flagIndex = 0;
            }

            cooldown = Delay;
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            if (!ResetFlags) return;
            for (int i = 0; i < Flags.Length; i++)
                foreach (string flag in Flags[i])
                    ((OriginalLevelSceneRoot)scene).Session.SetFlag(flag, false);
        }
    }
}
