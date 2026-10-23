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
        private struct ChainNode
        {
            public Vector2 Position, Velocity, Acceleration;

            public void UpdateStep(float deltaTime)
            {
                Velocity += Acceleration * deltaTime;
                Position += Velocity * deltaTime;
                Velocity *= 1 - deltaTime;
                Acceleration = Vector2.Zero;
            }

            public void ConstraintTo(Vector2 to, float distance, bool cancelAcceleration,
                ISourceVectorArithmetic arithmetic)
            {
                if (arithmetic.Distance(to, Position) > distance)
                {
                    Vector2 from = Position;
                    Vector2 dir = from - to;
                    dir = arithmetic.Normalize(dir);
                    Position = to + (dir * distance);
                    if (!cancelAcceleration)
                    {
                        Vector2 accel = Position - from;
                        accel.X = Math.Clamp(accel.X, -2f, 2f);
                        accel.Y = Math.Clamp(accel.Y, -2f, 2f);
                        Acceleration += accel * 210f;
                    }
                }
            }
        }
    }
}
