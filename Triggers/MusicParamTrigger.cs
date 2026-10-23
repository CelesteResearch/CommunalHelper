// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Processes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.CommunalHelper.Triggers
{
    [CustomEntity("CommunalHelper/MusicParamTrigger")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Triggers.MusicParamTrigger")]
    public sealed class MusicParamTrigger : SourceTriggerEntityState
    {
        private readonly IOriginalGameplayProcess process;
        private readonly float enterValue;
        private readonly float exitValue;
        private readonly string[] parameters;

        public MusicParamTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            process = (IOriginalGameplayProcess)engine;
            enterValue = data.Float("enterValue", 1);
            exitValue = data.Float("exitValue", 0);
            parameters = data.Attr("param").Split(',');
            for (int i = 0; i < parameters.Length; i++) parameters[i] = parameters[i].Trim();
        }

        private void SetParameters(float value)
        {
            foreach (string parameter in parameters) process.AudioSession.SetMusicParam(parameter, value);
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            SetParameters(enterValue);
        }

        public override void OnLeave(RoomPlayerSession player)
        {
            // The installed upstream trigger calls OnEnter on leave; preserve its PlayerIsInside state.
            base.OnEnter(player);
            SetParameters(exitValue);
        }
    }
}
