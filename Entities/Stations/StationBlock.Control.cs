// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Gameplay.Collision;
using Celeste.Physics.Collision;
using Celeste.Mods.CommunalHelper.Content.Stations;
using Celeste.Mods.CommunalHelper.Runtime.Hazards;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using SoundSource = Celeste.Simulation.Audio.Sources.SoundSourceComponentState;
using Player = Celeste.Simulation.RoomPlayerSession;
using DashCollisionResults = Celeste.Simulation.Gameplay.Player.Collision.Surfaces.DashCollisionResult;
using Node = Celeste.Mods.CommunalHelper.Entities.Stations.StationBlockTrack.Node;

namespace Celeste.Mods.CommunalHelper.Entities.Stations
{
    public partial class StationBlock
    {
        public void Attach(Node node)
        {
            IsAttachedToTrack = true;
            CurrentNode = node;

            if (node.NodeUp is not null)
                arrowDir = ArrowDir.Up;
            else if (node.NodeRight is not null)
                arrowDir = ArrowDir.Right;
            else if (node.NodeLeft is not null)
                arrowDir = ArrowDir.Left;
            else if (node.NodeDown is not null)
                arrowDir = ArrowDir.Down;

            arrowSprite.Play("Idle" + Enum.GetName(typeof(ArrowDir), arrowDir), true);
        }

        private DashCollisionResults OnDashed(Player player, Vector2 dir)
        {
            // Weird, lame fix, but eh.
            if (player.Machine.State == 5)
                player.Machine.State = 0;

            // Easier wall bounces.
            if ((player.Left >= Right - 4f || player.Right < Left + 4f) && dir.Y == -1 && dashCornerCorrection)
            {
                return DashCollisionResults.NormalCollision;
            }

            if (IsMoving || !IsAttachedToTrack || (PlayerCollidesWithSpikes(player) &&
                                                   !((IOriginalGameplayProcess)Engine).SaveContext.CurrentSave.Assists
                                                       .Invincible))
            {
                return DashCollisionResults.NormalCollision;
            }
            else
            {
                Smash(ReverseControls ? dir : -dir);

                bool inverted = TimedSpikeCapabilities.For(Engine).IsPlayerInverted?.Invoke() ?? false;

                if (allowWavedash && dir.Y == 1)
                {
                    colorLerp = 1f;
                    return !inverted ? DashCollisionResults.NormalCollision : DashCollisionResults.Rebound;
                }

                if (allowWavedashBottom && dir.Y == -1)
                {
                    bottomColorLerp = 1f;
                    return inverted ? DashCollisionResults.NormalCollision : DashCollisionResults.Rebound;
                }

                return DashCollisionResults.Rebound;
            }
        }

        private void Smash(Vector2 dir, bool force = false)
        {
            MoveDir = dir;

            if (MoveDir == -Vector2.UnitY && CurrentNode.NodeUp is not null)
            {
                nextNode = CurrentNode.NodeUp;
                currentTrack = CurrentNode.TrackUp;
            }
            else if (MoveDir == Vector2.UnitY && CurrentNode.NodeDown is not null)
            {
                nextNode = CurrentNode.NodeDown;
                currentTrack = CurrentNode.TrackDown;
            }
            else if (MoveDir == -Vector2.UnitX && CurrentNode.NodeLeft is not null)
            {
                nextNode = CurrentNode.NodeLeft;
                currentTrack = CurrentNode.TrackLeft;
            }
            else if (MoveDir == Vector2.UnitX && CurrentNode.NodeRight is not null)
            {
                nextNode = CurrentNode.NodeRight;
                currentTrack = CurrentNode.TrackRight;
            }
            else
            {
                nextNode = null;
                currentTrack = null;
            }

            IsMoving = force && (currentTrack?.CanBeUsed ?? false);
            if (!force)
            {
                scale = new Vector2(
                    1f + (Math.Abs(dir.Y) * 0.35f) - (Math.Abs(dir.X) * 0.35f),
                    1f + (Math.Abs(dir.X) * 0.35f) - (Math.Abs(dir.Y) * 0.35f));
                hitOffset = dir * 5f;
                IsMoving = true;
            }
        }

        private string GetTurnAnim(ArrowDir from, Vector2 dirTo)
        {
            ArrowDir to = ArrowDir.Down;
            if (dirTo == -Vector2.UnitX)
                to = ArrowDir.Left;
            if (dirTo == Vector2.UnitX)
                to = ArrowDir.Right;
            if (dirTo == -Vector2.UnitY)
                to = ArrowDir.Up;

            arrowDir = to;
            return GetAnimName(from, to);
        }

        private string GetAnimName(ArrowDir from, ArrowDir to)
        {
            return Enum.GetName(typeof(ArrowDir), from) + "To" + Enum.GetName(typeof(ArrowDir), to);
        }
    }
}
