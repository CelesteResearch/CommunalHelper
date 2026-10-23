// Copyright (c) 2020 Flynn Swainston-Calcutt. Licensed under the MIT License.

using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Platforms.MoveBlocks;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;

namespace Celeste.Mods.CommunalHelper.Runtime.MoveBlocks.Redirecting
{
    internal sealed class VanillaMoveBlockRedirectAdapter : IRedirectableMoveBlock
    {
        private readonly MoveBlockState block;

        public VanillaMoveBlockRedirectAdapter(MoveBlockState block) => this.block = block;

        public bool CanSteer => block.CanSteer;

        public float Speed
        {
            get => block.Speed;
            set => block.Speed = value;
        }

        public float TargetSpeed
        {
            get => block.TargetSpeed;
            set => block.TargetSpeed = value;
        }

        public MoveBlockDirection Direction
        {
            get => block.Direction;
            set => block.Direction = value;
        }

        public float Angle
        {
            get => block.Angle;
            set => block.Angle = value;
        }

        public float HomeAngle
        {
            get => block.HomeAngle;
            set => block.HomeAngle = value;
        }

        public float TargetAngle
        {
            get => block.TargetAngle;
            set => block.TargetAngle = value;
        }

        public SoundSourceComponentState MoveSfx => block.MovementSound;
        public void MoveTo(Vector2 position) => block.MoveTo(position);
        public void StartShaking(float duration) => block.StartShaking(duration);

        public void ResumeController(SimulationCoroutine coroutine) =>
            coroutine.Replace(block.ResumeMovementController());

        public void BreakController(SimulationCoroutine coroutine) =>
            coroutine.Replace(block.ResumeBreakingController());
    }
}
