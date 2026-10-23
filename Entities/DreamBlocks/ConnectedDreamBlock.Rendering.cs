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
        public override void Render(ISourceRenderContext context)
        {
            CameraState camera = ((OriginalLevelSceneRoot)Scene).Camera;
            Vector2 GroupPosition = new(GroupBoundsMin.X, GroupBoundsMin.Y);

            float whiteFill = SourceWhiteFill;
            float whiteHeight = SourceWhiteHeight;
            Vector2 shake = SourceShake;

            Vector2 pos = Position + GroupOffset + shake;

            if (!EverestCullHelper.IsRectangleVisible(SourceEngine, pos.X, pos.Y, GroupRect.Width, GroupRect.Height, 0,
                    camera))
            {
                return;
            }

            if (MasterOfGroup)
            {
                Color lineColor = PlayerHasDreamDash ? ActiveLineColor : DisabledLineColor;
                Color backColor = Color.Lerp(PlayerHasDreamDash ? SourceActiveBackColor : SourceDisabledBackColor,
                    Color.White, ColorLerp);

                if (whiteFill > 0f)
                {
                    lineColor = Color.Lerp(lineColor, Color.White, whiteFill);
                    if (whiteHeight == 1f)
                        backColor = Color.Lerp(backColor, Color.White, whiteFill);
                }

                #region Background rendering

                foreach (ConnectedDreamBlock block in Group)
                {
                    if (block.Right < camera.Left || block.Left > camera.Right || block.Bottom < camera.Top ||
                        block.Top > camera.Bottom)
                    {
                        continue;
                    }

                    SourcePrimitiveDrawing.Rect(context, this, block.Position + shake, block.Width, block.Height,
                        backColor);
                }

                #endregion

                #region Particle rendering

                for (int i = 0; i < particles.Length; i++)
                {
                    DreamParticle particle = particles[i];
                    int layer = particle.Layer;
                    Vector2 position = particle.Position + (camera.Position * (0.3f + (0.25f * layer)));
                    float rotation = 0;
                    MTexture particleTexture;
                    if (FeatherMode)
                    {
                        rotation = 1.5707963705062866f - 0.8f +
                                   (float)Math.Sin(particle.RotationCounter * particle.MaxRotate);
                        position += Vector2Geometry.AngleToVector(rotation, 4f);
                        particleTexture = featherTextures[layer];
                    }
                    else
                    {
                        MTexture[] sourceTextures = RefillCount != -1 ? doubleRefillStarTextures : particleTextures;
                        particleTexture = GetDreamParticleTexture(i, sourceTextures);
                    }

                    if (particleTexture is null)
                    {
                        particleTexture =
                            ((ISourceDrawScope)context).Draw
                            .Particle; // this ensures the nullcheck doesn't break the CullHelper, since that's a possible option
                    }

                    position = PutInside(position, GroupRect);
                    if (!EverestCullHelper.IsRectangleVisible(SourceEngine, position.X, position.Y,
                            particleTexture.FrameWidth, particleTexture.FrameHeight, 8, camera))
                        continue;

                    bool particleIsInside = false;
                    foreach (ConnectedDreamBlock block in Group)
                    {
                        if (block.CheckParticleCollide(position))
                        {
                            particleIsInside = true;
                            break;
                        }
                    }

                    if (!particleIsInside)
                        continue;

                    Color color = Color.Lerp(particle.Color, Color.Black, ColorLerp);
                    if (whiteFill > 0f && whiteHeight == 1f)
                        color = Color.Lerp(color, Color.White, whiteFill);

                    SourceAtlasDrawing.DrawCentered(context, this, particleTexture, position + Shake + shake, color, 1f,
                        rotation);
                }

                #endregion

                #region (De)activation Rendering

                if (whiteFill == 1f && whiteHeight < 1f)
                {
                    float whiteFillBottom = GroupRect.Y + (GroupRect.Height * whiteHeight);
                    foreach (ConnectedDreamBlock block in Group)
                    {
                        if (block.Right < camera.Left || block.Left > camera.Right || block.Bottom < camera.Top ||
                            block.Top > camera.Bottom)
                        {
                            continue;
                        }

                        if (block.Top <= whiteFillBottom)
                            SourcePrimitiveDrawing.Rect(context, this, block.Position + shake, block.Width,
                                ScalarMath.Clamp(whiteFillBottom - block.Y, 1f, block.Height), Color.White);
                    }
                }

                #endregion

                #region Edge & Corner Rendering

                if (whiteFill > 0f && whiteHeight < 1f)
                {
                    backColor = Color.Lerp(backColor, Color.White, whiteFill);
                }

                foreach (SpaceJamCorner corner in GroupCorners)
                {
                    // Yes.
                    RenderCorner(context, GroupPosition + shake, corner, lineColor, backColor);
                }

                foreach (SpaceJamEdge edge in GroupEdges)
                {
                    Vector2 start = edge.start, end = edge.end;
                    if (edge.flipNormal)
                    {
                        start = edge.end;
                        end = edge.start;

                        if (start.X == end.X)
                        {
                            start.X -= 1;
                            end.X -= 1;
                        }

                        if (start.Y == end.Y)
                        {
                            start.Y += 1;
                            end.Y += 1;
                        }
                    }

                    WobbleLine(context, GroupBoundsMin + start + shake, GroupBoundsMin + end + shake, edge.wobbleOffset,
                        lineColor, backColor);
                }

                #endregion
            }
        }

        private void RenderCorner(ISourceRenderContext context, Vector2 position, SpaceJamCorner corner, Color line,
            Color back)
        {
            int x = (int)((corner.x * 8) + position.X);
            int y = (int)((corner.y * 8) + position.Y);

            // Simple corners:
            if (corner.upright)
                SourcePrimitiveDrawing.Rect(context, this, x + 6, y, 2, 2, line);
            if (corner.upleft)
                SourcePrimitiveDrawing.Rect(context, this, x, y, 2, 2, line);
            if (corner.downright)
                SourcePrimitiveDrawing.Rect(context, this, x + 6, y + 6, 2, 2, line);
            if (corner.downleft)
                SourcePrimitiveDrawing.Rect(context, this, x, y + 6, 2, 2, line);

            // Inner corners:
            if (corner.inupright)
            {
                SourcePrimitiveDrawing.Rect(context, this, x + 6, y, 4, 3, back);
                SourcePrimitiveDrawing.Rect(context, this, x + 5, y - 1, 3, 3, back);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, x + 7, y, x + 10, y, line,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, x + 7, y, x + 7, y - 1, line,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
            }

            if (corner.inupleft)
            {
                SourcePrimitiveDrawing.Rect(context, this, x - 2, y, 4, 3, back);
                SourcePrimitiveDrawing.Rect(context, this, x, y - 1, 3, 3, back);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, x - 2, y, x, y, line,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, x, y + 1, x, y - 1, line,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
            }

            if (corner.indownright)
            {
                SourcePrimitiveDrawing.Rect(context, this, x + 6, y + 5, 4, 3, back);
                SourcePrimitiveDrawing.Rect(context, this, x + 5, y + 6, 3, 3, back);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, x + 7, y + 7, x + 10, y + 7, line,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, x + 8, y + 8, x + 8, y + 9, line,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
            }

            if (corner.indownleft)
            {
                SourcePrimitiveDrawing.Rect(context, this, x - 2, y + 5, 4, 3, back);
                SourcePrimitiveDrawing.Rect(context, this, x, y + 6, 3, 3, back);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, x - 2, y + 7, x + 1, y + 7, line,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, x + 1, y + 8, x + 1, y + 9, line,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
            }
        }

        private void WobbleLine(ISourceRenderContext context, Vector2 from, Vector2 to, float offset, Color line,
            Color back)
        {
            Vector2 vec = to - from;
            float length = ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic.Length(vec);
            Vector2 value = ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic.Normalize(vec);
            Vector2 vector = new(value.Y, 0f - value.X);

            float scaleFactor = 0f;
            int increment = 16;
            for (int i = 2; i < length - 2; i += increment)
            {
                float scale = ((ISourceScalarEnvironment)SourceEngine).ScalarArithmetic.Lerp(
                    GetDreamLineAmplitude(SourceWobbleFrom + offset, i),
                    GetDreamLineAmplitude(SourceWobbleTo + offset, i),
                    SourceWobbleEase);
                if (i + increment >= length)
                {
                    scale = 0f;
                }

                float num4 = Math.Min(increment, length - 2f - i);
                Vector2 vector2 = from + (value * i) + (vector * scaleFactor);
                Vector2 vector3 = from + (value * (i + num4)) + (vector * scale);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, vector2 - vector, vector3 - vector, back,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, vector2 - (vector * 2f), vector3 - (vector * 2f),
                    back, ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
                //SourcePrimitiveDrawing.Line((ISourceDrawScope)context, vector2 - vector * 3f, vector3 - vector * 3f, back, ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
                SourcePrimitiveDrawing.Line((ISourceDrawScope)context, vector2, vector3, line,
                    ((ISourceVectorEnvironment)SourceEngine).VectorArithmetic);
                scaleFactor = scale;
            }
        }
    }
}
