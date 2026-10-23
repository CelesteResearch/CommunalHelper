// SPDX-License-Identifier: MIT

using Celeste.Core.Scheduling;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedMoveBlock
    {
        private enum ControllerEntry
        {
            Normal,
            AfterMovementYield,
            AfterBreakShake
        }

        bool IRedirectableMoveBlock.CanSteer => false;

        float IRedirectableMoveBlock.Speed
        {
            get => speed;
            set => speed = value;
        }

        float IRedirectableMoveBlock.TargetSpeed
        {
            get => targetSpeed;
            set => targetSpeed = value;
        }

        MoveBlockDirection IRedirectableMoveBlock.Direction
        {
            get => Direction;
            set => Direction = value;
        }

        float IRedirectableMoveBlock.Angle
        {
            get => angle;
            set => angle = value;
        }

        float IRedirectableMoveBlock.HomeAngle
        {
            get => homeAngle;
            set => homeAngle = value;
        }

        float IRedirectableMoveBlock.TargetAngle
        {
            get => targetAngle;
            set => targetAngle = value;
        }

        SoundSourceComponentState IRedirectableMoveBlock.MoveSfx => moveSfx;

        // These entries replace original iterator state 4 and 5 without binding to compiler state numbers.
        public void ResumeController(SimulationCoroutine coroutine)
            => coroutine.Replace(Controller(ControllerEntry.AfterMovementYield));

        public void BreakController(SimulationCoroutine coroutine)
        {
            groupable.State = GroupableMoveBlock.MovementState.Breaking;
            coroutine.Replace(Controller(ControllerEntry.AfterBreakShake));
        }
    }
}
