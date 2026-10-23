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
        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (!HasGroup)
            {
                MasterOfGroup = true;
                Track = [];
                Group = [];
                AddToGroupAndFindChildren(this);

                bool multiBlock = false;
                foreach (StationBlockTrack t in Group)
                {
                    if (t.multiBlockTrack)
                    {
                        multiBlock = true;
                    }
                }

                if (dynamicRouting)
                {
                    Reroute(this);
                }

                List<Tuple<StationBlock, Node>> toAttach = new();
                bool exit = false;
                foreach (Node node in Track)
                {
                    foreach (StationBlock entity in StationResources.Tracked<StationBlock>(Scene))
                    {
                        if (!entity.IsAttachedToTrack &&
                            Math.Abs(node.Center.X - entity.Center.X) <= 4 &&
                            Math.Abs(node.Center.Y - entity.Center.Y) <= 4)
                        {
                            toAttach.Add(new Tuple<StationBlock, Node>(entity, node));
                            if (!multiBlock)
                            {
                                exit = true;
                                break;
                            }
                        }
                    }

                    if (exit)
                        break;
                }

                if (toAttach.Count == 0)
                {
                    SetTrackTheme(StationBlock.Themes.Normal, false);
                }
                else
                {
                    bool setTheme = false;
                    foreach (Tuple<StationBlock, Node> tuple in toAttach)
                    {
                        // Found block(s) to attach.
                        Node node = tuple.Item2;
                        StationBlock block = tuple.Item1;

                        block.Attach(node);
                        if (!setTheme)
                        {
                            OffsetTrack(block.Center - node.Center);
                            SetTrackTheme(block.Theme, block.ReverseControls, block.CustomNode, block.CustomTrackH,
                                block.CustomTrackV);
                            setTheme = true;
                        }

                        block.Position += node.Center - block.Center;
                    }
                }
            }
        }

        private void SetTrackTheme(StationBlock.Themes theme, bool reversedControls, MTexture customNode = null,
            MTexture customTrackH = null, MTexture customTrackV = null)
        {
            if (customNode is null && customTrackH is null && customTrackV is null)
            {
                string node, trackV, trackH;
                bool constantLooping;
                switch (theme)
                {
                    default:
                    case StationBlock.Themes.Normal:
                        constantLooping = false;
                        if (reversedControls)
                        {
                            node = "altTrack/ball";
                            trackV = "altTrack/pipeV";
                            trackH = "altTrack/pipeH";
                        }
                        else
                        {
                            node = "track/ball";
                            trackV = "track/pipeV";
                            trackH = "track/pipeH";
                        }

                        break;

                    case StationBlock.Themes.Moon:
                        constantLooping = true;
                        if (reversedControls)
                        {
                            node = "altMoonTrack/node";
                            trackV = "altMoonTrack/trackV";
                            trackH = "altMoonTrack/trackH";
                        }
                        else
                        {
                            node = "moonTrack/node";
                            trackV = "moonTrack/trackV";
                            trackH = "moonTrack/trackH";
                        }

                        break;
                }

                foreach (StationBlockTrack track in Group)
                {
                    track.trackConstantLooping = constantLooping;
                    track.trackSprite = Atlas[TracksPath + (track.Horizontal ? trackH : trackV)];
                    track.disabledTrackSprite = Atlas[TracksPath + "outline/" + (track.Horizontal ? "h" : "v")];
                    track.nodeSprite = Atlas.GetAtlasSubtextures(TracksPath + node);
                }
            }
            else
            {
                foreach (StationBlockTrack track in Group)
                {
                    track.trackSprite = track.Horizontal ? customTrackH : customTrackV;
                    track.nodeSprite = new List<MTexture>() { customNode };
                }
            }
        }

        private void OffsetTrack(Vector2 amount)
        {
            foreach (Node node in Track)
            {
                node.Position += amount;
                node.Center += amount;
            }

            foreach (StationBlockTrack track in Group)
            {
                track.Position += amount;
            }
        }

        private void AddToGroupAndFindChildren(StationBlockTrack from)
        {
            from.HasGroup = true;
            from.master = this;
            Group.Add(from);

            AddTrackSegmentToTrack(from.initialNodeData1, from.initialNodeData2, from);

            foreach (StationBlockTrack track in StationResources.Tracked<StationBlockTrack>(Scene))
            {
                if (!track.HasGroup && !from.trackRect.Intersects(track.trackRect))
                {
                    if (from.nodeRect1.Intersects(track.nodeRect1) || from.nodeRect1.Intersects(track.nodeRect2) ||
                        from.nodeRect2.Intersects(track.nodeRect1) || from.nodeRect2.Intersects(track.nodeRect2))
                    {
                        AddToGroupAndFindChildren(track);
                    }
                }
            }
        }

        private static Node GetNodeAt(List<Node> lookAt, Vector2 pos)
        {
            foreach (Node node in lookAt)
            {
                if (node.Position == pos)
                {
                    return node;
                }
            }

            return null;
        }

        private Node AddNodeToTrack(Node node, StationBlockTrack track)
        {
            Node foundNode = GetNodeAt(Track, node.Position);

            if (foundNode is null)
            {
                Track.Add(node);
                return node;
            }
            else
            {
                if (foundNode.NodeUp is null && node.NodeUp is not null)
                {
                    foundNode.NodeUp = node.NodeUp;
                    node.NodeUp.NodeDown = foundNode;
                    foundNode.TrackUp = track;
                    node.NodeUp.TrackDown = track;
                }

                if (foundNode.NodeDown is null && node.NodeDown is not null)
                {
                    foundNode.NodeDown = node.NodeDown;
                    node.NodeDown.NodeUp = foundNode;
                    foundNode.TrackDown = track;
                    node.NodeDown.TrackUp = track;
                }

                if (foundNode.NodeLeft is null && node.NodeLeft is not null)
                {
                    foundNode.NodeLeft = node.NodeLeft;
                    node.NodeLeft.NodeRight = foundNode;
                    foundNode.TrackLeft = track;
                    node.NodeLeft.TrackRight = track;
                }

                if (foundNode.NodeRight is null && node.NodeRight is not null)
                {
                    foundNode.NodeRight = node.NodeRight;
                    node.NodeRight.NodeLeft = foundNode;
                    foundNode.TrackRight = track;
                    node.NodeRight.TrackLeft = track;
                }

                if (foundNode.PushForce == Vector2.Zero && node.PushForce != Vector2.Zero)
                {
                    foundNode.PushForce = node.PushForce;
                    foundNode.HasIndicator = node.HasIndicator;
                    foundNode.IndicatorColor = node.IndicatorColor;
                    foundNode.IndicatorIncomingColor = node.IndicatorIncomingColor;
                }

                return foundNode;
            }
        }

        private void AddTrackSegmentToTrack(Node node1, Node node2, StationBlockTrack track)
        {
            track.node1 = AddNodeToTrack(node1, track);
            track.node2 = AddNodeToTrack(node2, track);
        }
    }
}
