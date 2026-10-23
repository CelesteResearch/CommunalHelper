// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Entities.Platforms.ZipMovers.Motion;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Platforms.ZipMovers;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedZipMover
    {
        [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.ConnectedZipMover+PathRenderer")]
        public class PathRenderer : SceneEntityState
        {
            [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.ConnectedZipMover+PathRenderer+Segment")]
            private class Segment
            {
                public bool Seen { get; set; }

                private readonly Vector2 from, to;
                private readonly Vector2 dir, twodir, perp;
                private readonly float length;

                private readonly Vector2 lineStartA, lineStartB;
                private readonly Vector2 lineEndA, lineEndB;

                public Rectangle Bounds { get; }

                private readonly Vector2 sparkAdd;

                private readonly float sparkDirStartA, sparkDirStartB;
                private readonly float sparkDirEndA, sparkDirEndB;
                private const float piOverEight = ((float)Math.PI / 4f) / 2f;
                private const float eightPi = 4 * ((float)Math.PI * 2f);

                public Segment(ISourceEngineContext engine, Vector2 from, Vector2 to)
                {
                    this.from = from;
                    this.to = to;

                    dir = ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(to - from);
                    twodir = 2 * dir;
                    perp = Perpendicular(dir);
                    length = ((ISourceVectorEnvironment)engine).VectorArithmetic.Distance(from, to);

                    Vector2 threeperp = 3 * perp;
                    Vector2 minusfourperp = -4 * perp;

                    lineStartA = from + threeperp;
                    lineStartB = from + minusfourperp;
                    lineEndA = to + threeperp;
                    lineEndB = to + minusfourperp;

                    sparkAdd = Perpendicular(
                        ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(from - to, 5f));
                    float angle = (float)Math.Atan2((from - to).Y, (from - to).X);
                    sparkDirStartA = angle + piOverEight;
                    sparkDirStartB = angle - piOverEight;
                    sparkDirEndA = angle + (float)Math.PI - piOverEight;
                    sparkDirEndB = angle + (float)Math.PI + piOverEight;

                    var minimum = new Vector2(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y));
                    var maximum = new Vector2(Math.Max(from.X, to.X), Math.Max(from.Y, to.Y));
                    var size = maximum - minimum;
                    Rectangle b = new((int)minimum.X, (int)minimum.Y, (int)size.X, (int)size.Y);
                    b.Inflate(10, 10);

                    Bounds = b;
                }

                public void Spark(PathRenderer owner, OriginalLevelSceneRoot level)
                {
                    ZipMoverDrawing.Sparks(owner, level.EngineContext, from, to, sparkAdd,
                        sparkDirStartA, sparkDirStartB, sparkDirEndA, sparkDirEndB);
                }

                public void Render(ISourceRenderContext context, object owner, ISourceEngineContext engine,
                    float percent,
                    Color rope, Color lightRope)
                {
                    ZipMoverDrawing.RopeLines(context, owner, ((ISourceVectorEnvironment)engine).VectorArithmetic,
                        lineStartA, lineStartB, lineEndA, lineEndB, dir, perp, length, percent * eightPi, null, rope,
                        lightRope);
                }

                public void RenderShadow(ISourceRenderContext context, object owner, ISourceEngineContext engine,
                    float percent)
                {
                    ZipMoverDrawing.RopeLines(context, owner, ((ISourceVectorEnvironment)engine).VectorArithmetic,
                        lineStartA + Vector2.UnitY, lineStartB + Vector2.UnitY, lineEndA + Vector2.UnitY,
                        lineEndB + Vector2.UnitY,
                        dir, perp, length, percent * eightPi, null, Color.Black, Color.Black);
                }
            }

            private readonly Rectangle bounds;
            private readonly Segment[] segments;

            private readonly ConnectedZipMover zipMover;

            private OriginalLevelSceneRoot level;

            private readonly Color color, lightColor;

            private readonly Vector2[] nodes;

            public PathRenderer(ConnectedZipMover zipMover, Vector2[] nodes, Color color, Color lightColor)
                : base(((Celeste.Core.Components.ISourceEntitySourceDataEnvironment)zipMover.Engine)
                    .EntitySourceDataRegistration)
            {
                this.zipMover = zipMover;

                this.nodes = new Vector2[nodes.Length];

                Vector2 offset = new(zipMover.MasterWidth / 2f, zipMover.MasterHeight / 2f);

                Vector2 prev = this.nodes[0] = nodes[0] + offset;
                Vector2 min = prev, max = prev;

                segments = new Segment[nodes.Length - 1];
                for (int i = 0; i < segments.Length; ++i)
                {
                    Vector2 node = this.nodes[i + 1] = nodes[i + 1] + offset;
                    segments[i] = new(zipMover.Engine, node, prev);

                    min = new Vector2(Math.Min(min.X, node.X), Math.Min(min.Y, node.Y));
                    max = new Vector2(Math.Max(max.X, node.X), Math.Max(max.Y, node.Y));

                    prev = node;
                }

                bounds = new((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y));
                bounds.Inflate(10, 10);

                this.color = color;
                this.lightColor = lightColor;

                Depth = 5000;
            }

            public override void Added(object scene)
            {
                base.Added(scene);
                level = scene as OriginalLevelSceneRoot;
            }

            public void CreateSparks()
            {
                foreach (Segment seg in segments)
                    seg.Spark(this, level);
            }

            public override void Render(ISourceRenderContext context)
            {
                Rectangle cameraBounds = CameraBounds(level);

                if (cameraBounds.Intersects(bounds))
                {
                    foreach (Segment seg in segments)
                        if (seg.Seen = cameraBounds.Intersects(seg.Bounds))
                            seg.RenderShadow(context, this, zipMover.Engine, zipMover.percent);

                    foreach (Segment seg in segments)
                        if (seg.Seen)
                            seg.Render(context, this, zipMover.Engine, zipMover.percent, color, lightColor);

                    float rotation = zipMover.percent * ((float)Math.PI * 2f);
                    foreach (Vector2 node in nodes)
                    {
                        SourceAtlasDrawing.DrawCentered(context, this, zipMover.cog, node + Vector2.UnitY, Color.Black,
                            1f,
                            rotation);
                        SourceAtlasDrawing.DrawCentered(context, this, zipMover.cog, node, Color.White, 1f, rotation);
                    }
                }

                // 'bounds' doesn't include the zip mover itself, it will do its own camera cull checks
                zipMover.DrawBorder(context);
            }
        }
    }
}
