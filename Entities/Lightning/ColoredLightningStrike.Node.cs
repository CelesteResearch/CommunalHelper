using Celeste.Core.Display;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Scene.Entities;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Graphics.Display;
using Celeste.Graphics.Rendering;

namespace Celeste.Mods.CommunalHelper.Entities.Lightning
{
    public partial class ColoredLightningStrike
    {
        private class Node
        {
            public Vector2 Position;

            public float Size;

            public List<Node> Children;

            public Node(float x, float y, float size)
                : this(new Vector2(x, y), size)
            {
            }

            public Node(Vector2 position, float size)
            {
                Position = position;
                Children = new List<Node>();
                Size = size;
            }

            public void Wiggle(ISourceRandom rand)
            {
                Position.X += SourceRandomMath.Range(rand, -2, 2);
                if (Position.Y != 0f)
                {
                    Position.Y += SourceRandomMath.Range(rand, -1, 1);
                }

                foreach (Node child in Children)
                {
                    child.Wiggle(rand);
                }
            }

            public void Render(ISourceRenderContext context, object actualEntity, ISourceVectorArithmetic arithmetic,
                Vector2 offset, float scale, PackedColor color)
            {
                float num = Size * scale;
                foreach (Node child in Children)
                {
                    Vector2 vector = arithmetic.SafeNormalize(child.Position - Position);
                    SourcePrimitiveDrawing.Line(context, actualEntity, offset + Position,
                        offset + child.Position + vector * num * 0.5f, color, num, arithmetic);
                    child.Render(context, actualEntity, arithmetic, offset, scale, color);
                }
            }
        }
    }
}
