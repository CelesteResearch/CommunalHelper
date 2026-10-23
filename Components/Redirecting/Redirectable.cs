// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;

namespace Celeste.Mods.CommunalHelper.Components.Redirecting
{
    [SceneTracked(true)]
    public abstract class Redirectable : SourceComponentState
    {
        public bool IsRedirectable { get; set; } = true;

        protected Redirectable() : base(false, false)
        {
        }

        public abstract float Speed { get; set; }
        public abstract float TargetSpeed { get; set; }
        public abstract MoveBlockDirection Direction { get; set; }
        public abstract float Angle { get; set; }
        public abstract void ResetBlock();
        public abstract void MoveTo(Vector2 to);
        public abstract void BeforeBreakAnimation();
        public abstract void OnBreak(SimulationCoroutine moveCoroutine);
        public abstract void OnPause(SimulationCoroutine moveCoroutine);
        public abstract void BeforeResumeAnimation();
        public abstract void OnResume(SimulationCoroutine moveCoroutine);
    }
}
