using Celeste.Core.Components;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Formats.Map.Entities.Structure;
using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Gameplay.Visibility;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Color = Celeste.Core.Display.PackedColor;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;

namespace Celeste.Mods.CommunalHelper.Entities.Chains
{
    public partial class Chain
    {
        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);

            if (!IsVisible())
                return;

            if (outline)
            {
                for (int i = 0; i < nodes.Length - 1; i++)
                {
                    if (Vector2Geometry.Round(nodes[i].Position) == Vector2Geometry.Round(nodes[i + 1].Position))
                        continue;

                    float yScale = arithmetic.Distance(nodes[i].Position, nodes[i + 1].Position) / distanceConstraint;
                    Vector2 mid = (nodes[i].Position + nodes[i + 1].Position) * 0.5f;
                    float angle = Vector2Geometry.Angle(nodes[i].Position, nodes[i + 1].Position) -
                                  ((float)Math.PI / 2f);
                    DrawOutlineOnlyCentered((ISourceDrawScope)context, segment, mid, new Vector2(1f, yScale), angle);
                }
            }

            for (int i = 0; i < nodes.Length - 1; i++)
            {
                if (Vector2Geometry.Round(nodes[i].Position) == Vector2Geometry.Round(nodes[i + 1].Position))
                    continue;

                float yScale = arithmetic.Distance(nodes[i].Position, nodes[i + 1].Position) / distanceConstraint;
                Vector2 mid = (nodes[i].Position + nodes[i + 1].Position) * 0.5f;
                float angle = Vector2Geometry.Angle(nodes[i].Position, nodes[i + 1].Position) - ((float)Math.PI / 2f);
                SourceAtlasDrawing.DrawCentered(context, this, segment, mid, Color.White, new Vector2(1f, yScale),
                    angle);
            }
        }

        public static void DrawChainLine(ISourceDrawScope scope, ISourceVectorArithmetic arithmetic, Vector2 from,
            Vector2 to, MTexture texture, bool outline)
        {
            Vector2 dir = arithmetic.Normalize(to - from);
            float angle = Vector2Geometry.Angle(dir) - ((float)Math.PI / 2f);
            float d = arithmetic.Distance(from, to);

            MTexture tip = texture.GetSubtexture(0, 0, 8, 8),
                mid = texture.GetSubtexture(0, 8, 8, 8);

            if (outline)
            {
                for (float t = d; t >= 0f; t -= 8f)
                    DrawOutlineOnlyCentered(scope, mid, Vector2.Lerp(from, to, t / d), Vector2.One, angle);
                DrawOutlineOnlyCentered(scope, tip, from + (dir * 4f), Vector2.One, angle);
            }

            for (float t = d; t >= 0f; t -= 8f)
                SourceAtlasDrawing.DrawCentered(scope, mid, Vector2.Lerp(from, to, t / d), Color.White, 1f, angle);
            SourceAtlasDrawing.DrawCentered(scope, tip, from + (dir * 4f), Color.White, 1f, angle);
        }
    }
}
