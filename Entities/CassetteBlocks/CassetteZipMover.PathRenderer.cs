// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Maps;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Core.Rendering;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Game.Gameplay.Interaction.Springs;
using Celeste.Game.Presentation.Platforms.Cassette;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Hazards.Spikes;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Mods.CommunalHelper.Presentation.Colors;
using Celeste.Mods.CommunalHelper.Runtime.CassetteBlocks;
using Color = Celeste.Core.Display.PackedColor;
using Image = Celeste.Graphics.Animation.SpriteImage;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Rectangle = Celeste.Core.Geometry.IntRect;
using System.Collections;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Game.Controls.Source;
using Celeste.Game.Entities.Platforms.ZipMovers.Motion;
using Celeste.Game.Presentation.Platforms.ZipMovers;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Presentation.Particles.Definitions;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    public partial class CassetteZipMover
    {
        [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CassetteZipMover+PathRenderer")]
        public class PathRenderer : SceneEntityState
        {
            private static Color baseRopeColor = SourceHexColor.HexToColor("bfcfde");
            private static Color baseRopeLightColor = SourceHexColor.HexToColor("ffffff");
            private static Color baseRopeColorPressed = SourceHexColor.HexToColor("324e69");
            private static Color baseRopeLightColorPressed = SourceHexColor.HexToColor("667da5");

            [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CassetteZipMover+PathRenderer+Segment")]
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

                public void Spark(PathRenderer owner, OriginalLevelSceneRoot level, ParticleType particle)
                {
                    ZipMoverDrawing.Sparks(owner, level.EngineContext, from, to, sparkAdd,
                        sparkDirStartA, sparkDirStartB, sparkDirEndA, sparkDirEndB, particle);
                }

                public void Render(ISourceRenderContext context, object owner, ISourceEngineContext engine,
                    float percent,
                    Vector2 offset, Color rope, Color lightRope)
                {
                    ZipMoverDrawing.RopeLines(context, owner, ((ISourceVectorEnvironment)engine).VectorArithmetic,
                        lineStartA + offset, lineStartB + offset, lineEndA + offset, lineEndB + offset,
                        dir, perp, length, percent * eightPi, null, rope, lightRope);
                }
            }

            private readonly Rectangle bounds;
            private readonly Segment[] segments;

            private readonly CassetteZipMover zipMover;

            private OriginalLevelSceneRoot level;

            private readonly Color ropeColor, ropeLightColor, ropeColorPressed, ropeLightColorPressed, undersideColor;
            private readonly ParticleType sparkParticle, sparkParticlePressed;

            private readonly Vector2[] nodes;

            public PathRenderer(CassetteZipMover zipMover, Vector2[] nodes)
                : base(((ISourceEntitySourceDataEnvironment)zipMover.EngineContext).EntitySourceDataRegistration)
            {
                this.zipMover = zipMover;

                this.nodes = new Vector2[nodes.Length];

                Vector2 offset = new(zipMover.Width / 2f, zipMover.Height / 2f);

                Vector2 prev = this.nodes[0] = nodes[0] + offset;
                Vector2 min = prev, max = prev;

                segments = new Segment[nodes.Length - 1];
                for (int i = 0; i < segments.Length; ++i)
                {
                    Vector2 node = this.nodes[i + 1] = nodes[i + 1] + offset;
                    segments[i] = new(zipMover.EngineContext, node, prev);

                    min = new Vector2(Math.Min(min.X, node.X), Math.Min(min.Y, node.Y));
                    max = new Vector2(Math.Max(max.X, node.X), Math.Max(max.Y, node.Y));

                    prev = node;
                }

                bounds = new((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y));
                bounds.Inflate(10, 10);

                ropeColor = CommunalColorChannels.Multiply(baseRopeColor, zipMover.color);
                ropeLightColor = CommunalColorChannels.Multiply(baseRopeLightColor, zipMover.color);
                ropeColorPressed = CommunalColorChannels.Multiply(baseRopeColorPressed, zipMover.color);
                ropeLightColorPressed = CommunalColorChannels.Multiply(baseRopeLightColorPressed, zipMover.color);
                undersideColor = ropeColorPressed;

                sparkParticle =
                    new ParticleType(((IOriginalGameplayProcess)zipMover.EngineContext).ParticleTypes.ZipMover.P_Sparks)
                        { Color = ropeLightColor };
                sparkParticlePressed =
                    new ParticleType(((IOriginalGameplayProcess)zipMover.EngineContext).ParticleTypes.ZipMover.P_Sparks)
                        { Color = ropeLightColorPressed };

                Depth = 5000;
            }

            public override void Added(object scene)
            {
                base.Added(scene);
                level = scene as OriginalLevelSceneRoot;
            }

            public void CreateSparks()
            {
                ParticleType p = zipMover.Collidable ? sparkParticle : sparkParticlePressed;
                foreach (Segment seg in segments)
                    seg.Spark(this, level, p);
            }

            public override void Update()
            {
                Depth = zipMover.Collidable ? 9000 : 9000 + 10;
            }

            public override void Render(ISourceRenderContext context)
            {
                Rectangle cameraBounds = CameraBounds(level);

                if (!cameraBounds.Intersects(bounds))
                    return;

                bool on = zipMover.Collidable;
                Color color = on ? ropeColor : ropeColorPressed;
                Color lightColor = on ? ropeLightColor : ropeLightColorPressed;

                foreach (Segment seg in segments)
                    seg.Seen = cameraBounds.Intersects(seg.Bounds);

                for (int i = 1; i <= zipMover.BlockHeight; ++i)
                {
                    Vector2 o = new(0, i + zipMover.BlockOffset.Y);
                    foreach (Segment seg in segments)
                        if (seg.Seen)
                            seg.Render(context, this, zipMover.EngineContext, zipMover.percent, o,
                                undersideColor * zipMover.sideAlpha, undersideColor * zipMover.sideAlpha);
                }

                foreach (Segment seg in segments)
                    if (seg.Seen)
                        seg.Render(context, this, zipMover.EngineContext, zipMover.percent, zipMover.BlockOffset, color,
                            lightColor);

                float rotation = zipMover.percent * ((float)Math.PI * 2f);
                MTexture cogTex = on ? zipMover.cog : zipMover.cogPressed;
                foreach (Vector2 node in nodes)
                {
                    for (int i = 1; i <= zipMover.BlockHeight; ++i)
                    {
                        Vector2 o = new(0, i + zipMover.BlockOffset.Y);
                        SourceAtlasDrawing.DrawCentered(context, this, zipMover.cogWhite, node + o,
                            undersideColor * zipMover.sideAlpha, 1f, rotation);
                    }

                    SourceAtlasDrawing.DrawCentered(context, this, cogTex, node + zipMover.BlockOffset, zipMover.color,
                        1f,
                        rotation);
                }
            }
        }
    }
}
