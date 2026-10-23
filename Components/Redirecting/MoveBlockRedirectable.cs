// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;

namespace Celeste.Mods.CommunalHelper.Components.Redirecting
{
    [SceneTracked(true)]
    public sealed class MoveBlockRedirectable : Redirectable
    {
        private readonly IRedirectableMoveBlock block;
        private readonly float initialAngle;
        private readonly MoveBlockDirection initialDirection;

        public MoveBlockRedirectable(IRedirectableMoveBlock block)
        {
            this.block = block;
            IsRedirectable = !block.CanSteer;
            initialAngle = Angle;
            initialDirection = Direction;
        }

        public override float Speed
        {
            get => block.Speed;
            set => block.Speed = value;
        }

        public override float TargetSpeed
        {
            get => block.TargetSpeed;
            set => block.TargetSpeed = value;
        }

        public override MoveBlockDirection Direction
        {
            get => block.Direction;
            set => block.Direction = value;
        }

        public override float Angle
        {
            get => block.Angle;
            set
            {
                block.Angle = value;
                block.HomeAngle = value;
                block.TargetAngle = value;
            }
        }

        public SoundSourceComponentState MoveSfx => block.MoveSfx;

        public override void ResetBlock()
        {
            Angle = initialAngle;
            Direction = initialDirection;
        }

        public override void MoveTo(Vector2 to) => block.MoveTo(to);

        public override void OnPause(SimulationCoroutine moveCoroutine)
        {
            MoveSfx?.Param("redirect_slowdown", 1f);
            block.StartShaking(0.2f);
        }

        public override void OnResume(SimulationCoroutine moveCoroutine) => block.ResumeController(moveCoroutine);
        public override void OnBreak(SimulationCoroutine moveCoroutine) => block.BreakController(moveCoroutine);
        public override void BeforeBreakAnimation() => MoveSfx?.Stop();

        public override void BeforeResumeAnimation()
        {
            block.StartShaking(0.18f);
            MoveSfx?.Param("redirect_slowdown", 0f);
        }
    }
}
