using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.Platforms.ZipMovers;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Rendering;
using Celeste.Mods.CommunalHelper.Content.Stations;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Rectangle = Celeste.Core.Geometry.IntRect;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;

namespace Celeste.Mods.CommunalHelper.Entities.Stations
{
    public partial class StationBlockTrack : SceneEntityState
    {
        public static void SwitchTracks(object scene, TrackSwitchState state)
        {
            foreach (StationBlockTrack track in StationResources.Tracked<StationBlockTrack>(scene))
            {
                if (track.MasterOfGroup)
                {
                    foreach (StationBlockTrack child in track.Group)
                    {
                        child.Switch(state);
                    }

                    if (track.dynamicRouting)
                    {
                        Reroute(track);
                    }
                }
            }
        }

        public static void ReverseTracks(object scene)
        {
            foreach (StationBlockTrack track in
                     StationResources.Tracked<StationBlockTrack>(scene).Cast<StationBlockTrack>())
            {
                if (track.MasterOfGroup)
                {
                    foreach (StationBlockTrack child in track.Group)
                    {
                        TrackMoveMode newMoveMode = child.Reverse();
                        if (newMoveMode == TrackMoveMode.BackwardForce)
                        {
                            if ((child.Horizontal && child.node1.PushForce == Vector2.UnitX)
                                || (!child.Horizontal && child.node1.PushForce == Vector2.UnitY))
                            {
                                child.node1.PushForce = Vector2.Zero;
                            }

                            child.node2.PushForce = child.Horizontal ? -Vector2.UnitX : -Vector2.UnitY;
                        }
                        else if (newMoveMode == TrackMoveMode.ForwardForce)
                        {
                            if ((child.Horizontal && child.node2.PushForce == -Vector2.UnitX)
                                || (!child.Horizontal && child.node2.PushForce == -Vector2.UnitY))
                            {
                                child.node2.PushForce = Vector2.Zero;
                            }

                            child.node1.PushForce = child.Horizontal ? Vector2.UnitX : Vector2.UnitY;
                        }
                    }
                }
            }
        }

        public static void Reroute(StationBlockTrack track)
        {
            foreach (StationBlockTrack child in track.Group)
            {
                if (child.dynamicRouting && child.moveMode is TrackMoveMode.ForwardForce or TrackMoveMode.BackwardForce)
                {
                    // we just need the outgoing node
                    Node node = child.moveMode == TrackMoveMode.ForwardForce ? child.node2 : child.node1;
                    if (node.PushForce != Vector2.Zero)
                    {
                        if (node.NodeUp is not null && node.TrackUp != child && node.TrackUp.CanBeUsed &&
                            node.TrackUp.moveMode == TrackMoveMode.BackwardForce)
                        {
                            node.PushForce = -Vector2.UnitY;
                        }
                        else if (node.NodeDown is not null && node.TrackDown != child && node.TrackDown.CanBeUsed &&
                                 node.TrackDown.moveMode == TrackMoveMode.ForwardForce)
                        {
                            node.PushForce = Vector2.UnitY;
                        }
                        else if (node.NodeLeft is not null && node.TrackLeft != child && node.TrackLeft.CanBeUsed &&
                                 node.TrackLeft.moveMode == TrackMoveMode.BackwardForce)
                        {
                            node.PushForce = -Vector2.UnitX;
                        }
                        else if (node.NodeRight is not null && node.TrackRight != child && node.TrackRight.CanBeUsed &&
                                 node.TrackRight.moveMode == TrackMoveMode.ForwardForce)
                        {
                            node.PushForce = Vector2.UnitX;
                        }
                    }
                }
            }
        }

        private void Switch(TrackSwitchState state)
        {
            if (initialSwitchState == TrackSwitchState.None)
                return;
            switchState = initialSwitchState == state ? TrackSwitchState.On : TrackSwitchState.Off;
        }

        private TrackMoveMode Reverse()
        {
            if (initialMoveMode == TrackMoveMode.None)
                return TrackMoveMode.None;

            moveMode = Invert(moveMode);
            OneWayDir = -OneWayDir;
            return moveMode;
        }

        public static TrackMoveMode Invert(TrackMoveMode moveMode)
        {
            return moveMode switch
            {
                TrackMoveMode.ForwardOneWay => TrackMoveMode.BackwardOneWay,
                TrackMoveMode.BackwardOneWay => TrackMoveMode.ForwardOneWay,
                TrackMoveMode.ForwardForce => TrackMoveMode.BackwardForce,
                TrackMoveMode.BackwardForce => TrackMoveMode.ForwardForce,
                _ => TrackMoveMode.None
            };
        }
    }
}
