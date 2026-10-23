// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;

namespace Celeste.Mods.CommunalHelper.Components.Redirecting
{
    public interface IRedirectableMoveBlock
    {
        bool CanSteer { get; }
        float Speed { get; set; }
        float TargetSpeed { get; set; }
        MoveBlockDirection Direction { get; set; }
        float Angle { get; set; }
        float HomeAngle { get; set; }
        float TargetAngle { get; set; }
        SoundSourceComponentState MoveSfx { get; }
        void MoveTo(Vector2 position);
        void StartShaking(float duration);
        void ResumeController(SimulationCoroutine coroutine);
        void BreakController(SimulationCoroutine coroutine);
    }
}
