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
        internal void SpawnFastRoutineParticles()
        {
            if (MasterOfGroup)
            {
                OriginalLevelSceneRoot level = ((OriginalLevelSceneRoot)Scene);
                foreach (SpaceJamEdge edge in GroupEdges)
                {
                    float width = edge.end.X - edge.start.X;
                    float centerH = edge.start.X + (width / 2f);
                    float height = edge.end.Y - edge.start.Y;
                    float centerV = edge.start.Y + (height / 2f);
                    if (edge.facing == Edges.North)
                        level.ParticlesFG.Emit(
                            ((IOriginalGameplayProcess)SourceEngine).ParticleTypes.Strawberry.P_WingsBurst, (int)width,
                            new Vector2(centerH, edge.start.Y), Vector2.UnitX * width / 2f, Color.White,
                            (float)Math.PI);
                    if (edge.facing == Edges.South)
                        level.ParticlesFG.Emit(
                            ((IOriginalGameplayProcess)SourceEngine).ParticleTypes.Strawberry.P_WingsBurst, (int)width,
                            new Vector2(centerH, edge.end.Y), Vector2.UnitX * width / 2f, Color.White, 0f);
                    if (edge.facing == Edges.West)
                        level.ParticlesFG.Emit(
                            ((IOriginalGameplayProcess)SourceEngine).ParticleTypes.Strawberry.P_WingsBurst, (int)height,
                            new Vector2(edge.start.X, centerV), Vector2.UnitY * height / 2f, Color.White, 4.712389f);
                    if (edge.facing == Edges.East)
                        level.ParticlesFG.Emit(
                            ((IOriginalGameplayProcess)SourceEngine).ParticleTypes.Strawberry.P_WingsBurst, (int)height,
                            new Vector2(edge.end.X, centerV), Vector2.UnitY * height / 2f, Color.White,
                            (float)Math.PI / 2f);
                }
            }
        }

        internal void SpawnSlowRoutineParticles()
        {
            if (MasterOfGroup)
            {
                OriginalLevelSceneRoot level = ((OriginalLevelSceneRoot)Scene);
                CameraState camera = level.Camera;

                float whiteHeight = SourceWhiteHeight;
                float whiteFillBottom = GroupRect.Y + (GroupRect.Height * whiteHeight);

                foreach (ConnectedDreamBlock block in Group)
                {
                    if (block.Right < camera.Left || block.Left > camera.Right || block.Bottom < camera.Top ||
                        block.Top > camera.Bottom)
                        continue;

                    if (block.Top <= whiteFillBottom && block.Bottom >= whiteFillBottom)
                    {
                        for (int i = 0; i < block.Width; i += 4)
                            level.ParticlesFG.Emit(
                                ((IOriginalGameplayProcess)SourceEngine).ParticleTypes.Strawberry.P_WingsBurst,
                                new Vector2(block.X + i, whiteFillBottom + 1f));
                    }
                }
            }
        }

        private bool TileHasGroupDreamBlock(int x, int y)
        {
            IntRect rect = TileToIntRect(x, y);
            rect.X += (int)GroupBoundsMin.X;
            rect.Y += (int)GroupBoundsMin.Y;
            foreach (ConnectedDreamBlock block in Group)
            {
                if (SourceEntityGeometryCollision.Rectangle(block, rect))
                    return true;
            }

            return false;
        }

        private static IntRect TileToIntRect(int x, int y)
        {
            Vector2 p = TileToPoint(x, y);
            return new IntRect((int)p.X, (int)p.Y, 8, 8);
        }

        private static Vector2 TileToPoint(int x, int y)
            => new(x * 8, y * 8);

        public void ConnectedFootstepRipple(Vector2 position)
        {
            if (PlayerHasDreamDash)
            {
                ConnectedDreamBlock master = MasterOfGroup ? this : this.master;

                foreach (ConnectedDreamBlock block in master.Group)
                {
                    DisplacementBurst burst =
                        ((OriginalLevelSceneRoot)Scene).Displacement.AddBurst(position, 0.5f, 0f, 40f);
                    burst.WorldClipCollider = block.Collider;
                    burst.WorldClipPadding = 1;
                }
            }
        }
    }
}
