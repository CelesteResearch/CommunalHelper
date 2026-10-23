// SPDX-License-Identifier: MIT

using System.Collections;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Mods.CommunalHelper.Entities.MoveBlocks;

namespace Celeste.Mods.CommunalHelper.Components.MoveBlocks
{
    /// <summary>
    /// Component used to allow Move Block Groups to contain any move block entity.
    /// </summary>
    [SceneTracked]
    public class GroupableMoveBlock : SourceComponentState
    {
        public enum MovementState
        {
            Idling,
            Moving,
            Breaking,
        }

        public MovementState State;

        // Cassette move blocks opt in because they are initially noncollidable.
        public bool AllowGroupingWhenNoncollidable { get; set; }

        public MoveBlockGroup Group { get; internal set; }
        public bool GroupTriggerSignal { get; internal set; }
        public bool WaitingForRespawn { get; internal set; }

        public GroupableMoveBlock() : base(true, false)
        {
        }

        // Both of these need to be SwapImmediately'ed in order to not introduce an extra frame of delay which would desync with previous behaviour.

        public IEnumerator SyncGroupTriggers()
        {
            if (Group is not null && Group.SyncActivation)
            {
                if (!GroupTriggerSignal)
                {
                    Group.Trigger(); // block was manually triggered
                }

                // ensures all moveblock in the group start simultaneously
                while (!GroupTriggerSignal) // wait for signal to come back
                    yield return null;
                GroupTriggerSignal = false; // reset
            }
        }

        public IEnumerator WaitForRespawn()
        {
            if (Group is not null)
            {
                WaitingForRespawn = true;
                while (!Group.CanRespawn(this))
                    yield return null;
            }
        }

        public PackedColor HighlightColor(PackedColor? color = null)
        {
            PackedColor basePackedColor = color ?? PackedColor.Transparent;
            return Group is null || Group.DisablePulse
                ? basePackedColor
                : PackedColor.Lerp(basePackedColor, Group.Color,
                    ((float)System.Math.Sin(((ISourceScene)Scene).SourceScene.Clock.TimeActive * 3) + 1f) / 2f);
        }
    }
}
