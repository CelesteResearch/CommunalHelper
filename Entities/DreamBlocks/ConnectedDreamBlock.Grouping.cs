using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Audio;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Gameplay.Visibility;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Graphics.Rendering.Drawing;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Particles;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Geometry.TwoD.Camera;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Presentation.PostProcessing.Displacement;
using Color = Celeste.Core.Display.PackedColor;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    public partial class ConnectedDreamBlock
    {
        public override void Awake(object scene)
        {
            base.Awake(scene);

            if (!HasGroup)
            {
                /* Setup group */
                MasterOfGroup = true;

                Moves = [];
                Group = [];
                JumpThrus = [];

                GroupBoundsMin = new Vector2(X, Y);
                GroupBoundsMax = new Vector2(Right, Bottom);
                GroupEdges = [];
                GroupCorners = [];
                AddToGroupAndFindChildren(this);
                SetupCustomParticles(0, 0); // Parameters are ignored

                GroupOffset = new Vector2(GroupBoundsMin.X, GroupBoundsMin.Y) - Position;

                float groupW = GroupBoundsMax.X - GroupBoundsMin.X;
                float groupH = GroupBoundsMax.Y - GroupBoundsMin.Y;

                /* Setup Edges of the group */
                int groupTileW = (int)(groupW / 8.0f);
                int groupTileH = (int)(groupH / 8.0f);
                SpaceJamTile[,] tiles = new SpaceJamTile[(groupTileW + 2), (groupTileH + 2)];
                for (int x = 0; x < groupTileW + 2; x++)
                for (int y = 0; y < groupTileH + 2; y++)
                    tiles[x, y] = new SpaceJamTile(x - 1, y - 1, TileHasGroupDreamBlock(x - 1, y - 1));

                for (int x = 1; x < groupTileW + 1; x++)
                for (int y = 1; y < groupTileH + 1; y++)
                    if (tiles[x, y].Exist)
                        AutoEdge(tiles, x, y);

                Vector2 groupCenter = new(groupW / 2, groupH / 2);
                for (int i = 0; i < GroupEdges.Count; i++)
                {
                    SpaceJamEdge edge = GroupEdges[i];
                    float angle = Vector2Geometry.Angle(groupCenter, Vector2.Lerp(edge.start, edge.end, 0.5f)) +
                                  (float)Math.PI;
                    GroupEdges[i] = new SpaceJamEdge(edge.start, edge.end, edge.wobbleOffset + angle, edge.flipNormal,
                        edge.facing);
                }
            }
        }

        public override void SetupCustomParticles(float canvasWidth, float canvasHeight)
        {
            if (MasterOfGroup)
                base.SetupCustomParticles(GroupBoundsMax.X - GroupBoundsMin.X, GroupBoundsMax.Y - GroupBoundsMin.Y);
        }

        protected override void UpdateParticles()
        {
            if (MasterOfGroup)
                base.UpdateParticles();
        }

        private void AutoEdge(SpaceJamTile[,] tiles, int x, int y)
        {
            SpaceJamTile self = tiles[x, y];
            SpaceJamTile nNorth = tiles[x, y - 1];
            SpaceJamTile nEast = tiles[x + 1, y];
            SpaceJamTile nSouth = tiles[x, y + 1];
            SpaceJamTile nWest = tiles[x - 1, y];
            SpaceJamTile nSouthEast = tiles[x + 1, y + 1];
            SpaceJamTile nSouthWest = tiles[x - 1, y + 1];
            SpaceJamTile nNorthEast = tiles[x + 1, y - 1];
            SpaceJamTile nNorthWest = tiles[x - 1, y - 1];

            #region Corner stuff

            bool upright = !nNorth.Exist && !nEast.Exist;
            bool upleft = !nNorth.Exist && !nWest.Exist;
            bool downright = !nSouth.Exist && !nEast.Exist;
            bool downleft = !nSouth.Exist && !nWest.Exist;
            bool inupright = nNorth.Exist && nEast.Exist && !nNorthEast.Exist;
            bool inupleft = nNorth.Exist && nWest.Exist && !nNorthWest.Exist;
            bool indownright = nSouth.Exist && nEast.Exist && !nSouthEast.Exist;
            bool indownleft = nSouth.Exist && nWest.Exist && !nSouthWest.Exist;
            if (upright || upleft || downright || downleft || inupright || inupleft || indownright || indownleft)
            {
                GroupCorners.Add(new SpaceJamCorner(x - 1, y - 1, upright, upleft, downright, downleft, inupright,
                    inupleft, indownright, indownleft));
            }

            #endregion

            if (!nNorth.Exist)
            {
                if (nWest.TryGetEdge(Edges.North, out int idx))
                {
                    SpaceJamEdge edge = GroupEdges[idx];
                    edge.end.X += 8;
                    GroupEdges[idx] = edge;
                    self[Edges.North] = idx;
                }
                else
                {
                    SpaceJamEdge newEdge;
                    newEdge.end = newEdge.start = TileToPoint(x - 1, y - 1);
                    newEdge.end.X += 8;
                    newEdge.wobbleOffset = 0.0f;
                    newEdge.flipNormal = false;
                    newEdge.facing = Edges.North;
                    self[Edges.North] = GroupEdges.Count;
                    GroupEdges.Add(newEdge);
                }
            }

            if (!nEast.Exist)
            {
                if (nNorth.TryGetEdge(Edges.East, out int idx))
                {
                    SpaceJamEdge edge = GroupEdges[idx];
                    edge.end.Y += (nSouth.Exist && nSouthEast.Exist) ? 9 : 8;
                    GroupEdges[idx] = edge;
                    self[Edges.East] = idx;
                }
                else
                {
                    SpaceJamEdge newEdge;
                    newEdge.end = newEdge.start = TileToPoint(x, y - 1);
                    newEdge.end.Y += (nSouth.Exist && nSouthEast.Exist) ? 9 : 8;
                    if (nNorth.Exist)
                    {
                        newEdge.start.Y -= 1;
                    }

                    newEdge.wobbleOffset = 0.7f;
                    newEdge.flipNormal = false;
                    newEdge.facing = Edges.East;
                    self[Edges.East] = GroupEdges.Count;
                    GroupEdges.Add(newEdge);
                }
            }

            if (!nSouth.Exist)
            {
                if (nWest.TryGetEdge(Edges.South, out int idx))
                {
                    SpaceJamEdge edge = GroupEdges[idx];
                    edge.end.X += 8;
                    GroupEdges[idx] = edge;
                    self[Edges.South] = idx;
                }
                else
                {
                    SpaceJamEdge newEdge;
                    newEdge.start = TileToPoint(x - 1, y);
                    newEdge.start.Y -= 1;
                    newEdge.end = newEdge.start;
                    newEdge.end.X += 8;
                    newEdge.wobbleOffset = 1.5f;
                    newEdge.flipNormal = true;
                    newEdge.facing = Edges.South;
                    self[Edges.South] = GroupEdges.Count;
                    GroupEdges.Add(newEdge);
                }
            }

            if (!nWest.Exist)
            {
                if (nNorth.TryGetEdge(Edges.West, out int idx))
                {
                    SpaceJamEdge edge = GroupEdges[idx];
                    edge.end.Y += (nSouth.Exist && nSouthWest.Exist) ? 9 : 8;
                    GroupEdges[idx] = edge;
                    self[Edges.West] = idx;
                }
                else
                {
                    SpaceJamEdge newEdge;
                    newEdge.start = TileToPoint(x - 1, y - 1);
                    newEdge.start.X += 1;
                    newEdge.end = newEdge.start;
                    newEdge.end.Y += (nSouth.Exist && nSouthWest.Exist) ? 9 : 8;
                    if (nNorth.Exist)
                    {
                        newEdge.start.Y -= 1;
                    }

                    newEdge.wobbleOffset = 2.5f;
                    newEdge.flipNormal = true;
                    newEdge.facing = Edges.West;
                    self[Edges.West] = GroupEdges.Count;
                    GroupEdges.Add(newEdge);
                }
            }
        }

        private void AddToGroupAndFindChildren(ConnectedDreamBlock from)
        {
            if (from.X < GroupBoundsMin.X)
                GroupBoundsMin.X = from.X;

            if (from.Y < GroupBoundsMin.Y)
                GroupBoundsMin.Y = from.Y;

            if (from.Right > GroupBoundsMax.X)
                GroupBoundsMax.X = from.Right;

            if (from.Bottom > GroupBoundsMax.Y)
                GroupBoundsMax.Y = from.Bottom;


            from.HasGroup = true;
            Group.Add(from);
            Moves.Add(from, from.Position);
            if (from != this)
            {
                from.master = this;
            }

            if (IncludeJumpThrus)
            {
                foreach (SourceJumpThruEntityState jumpThru in SourceSceneCollision.AllRect<SourceJumpThruEntityState>(
                             ((ISourceScene)Scene).SourceScene.Entities, "Celeste.JumpThru",
                             new IntRect((int)from.X - 1, (int)from.Y, (int)from.Width + 2, (int)from.Height)))
                {
                    if (!JumpThrus.Contains(jumpThru))
                        AddJumpThru(jumpThru);
                }

                foreach (SourceJumpThruEntityState jumpThru in SourceSceneCollision.AllRect<SourceJumpThruEntityState>(
                             ((ISourceScene)Scene).SourceScene.Entities, "Celeste.JumpThru",
                             new IntRect((int)from.X, (int)from.Y - 1, (int)from.Width, (int)from.Height + 2)))
                {
                    if (!JumpThrus.Contains(jumpThru))
                        AddJumpThru(jumpThru);
                }
            }

            foreach (SceneEntityState entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(ConnectedDreamBlock)))
            {
                ConnectedDreamBlock connectedBlock = (ConnectedDreamBlock)entity;
                if (!connectedBlock.HasGroup && connectedBlock.FeatherMode == from.FeatherMode &&
                    SourceSceneCollision.CheckRect(
                        new IntRect((int)from.X, (int)from.Y, (int)from.Width, (int)from.Height), connectedBlock))
                {
                    AddToGroupAndFindChildren(connectedBlock);
                }
            }
        }

        private void AddJumpThru(SourceJumpThruEntityState jp)
        {
            JumpThrus.Add(jp);
            Moves.Add(jp, jp.Position);
            foreach (SceneEntityState entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(ConnectedDreamBlock)))
            {
                ConnectedDreamBlock connectedBlock = (ConnectedDreamBlock)entity;
                if (!connectedBlock.HasGroup && connectedBlock.FeatherMode == FeatherMode &&
                    SourceSceneCollision.CheckRect(
                        new IntRect((int)jp.X - 1, (int)jp.Y, (int)jp.Width + 2, (int)jp.Height), connectedBlock))
                {
                    AddToGroupAndFindChildren(connectedBlock);
                }
            }
        }

        protected virtual DashCollisionResult OnDash(RoomPlayerSession player, Vector2 dir)
            => PlayerHasDreamDash
                ? DashCollisionResult.NormalOverride
                : DashCollisionResult.NormalCollision;

        protected virtual DashCollisionResult OnDashJumpThru(RoomPlayerSession player, Vector2 dir)
            => PlayerHasDreamDash
                ? DashCollisionResult.NormalOverride
                : DashCollisionResult.NormalCollision;

        public override void MoveHExact(int move)
        {
            base.MoveHExact(move);
            GroupBoundsMax.X += move;
            GroupBoundsMin.X += move;
        }

        public override void MoveVExact(int move)
        {
            base.MoveVExact(move);
            GroupBoundsMax.Y += move;
            GroupBoundsMin.Y += move;
        }
    }
}
