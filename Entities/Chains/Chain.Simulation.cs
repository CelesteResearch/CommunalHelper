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
        private void UpdateChain(bool lockNodes = false)
        {
            bool startAttached = attachedStartGetter is not null;
            bool endAttached = attachedEndGetter is not null;
            if (startAttached)
            {
                nodes[0].Position = attachedStartGetter();
                nodes[0].Velocity = Vector2.Zero;
            }

            if (endAttached)
            {
                nodes[^1].Position = attachedEndGetter();
                nodes[^1].Velocity = Vector2.Zero;
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i].Acceleration += Vector2.UnitY * 220f;
                if (Scene is not null && !Tight)
                    nodes[i].Acceleration += ((OriginalLevelSceneRoot)Scene).Wind;
                nodes[i].UpdateStep(engine.DeltaTime);
            }

            if (!startAttached && !endAttached)
            {
                for (int i = 1; i < nodes.Length; i++)
                    nodes[i].ConstraintTo(nodes[i - 1].Position, distanceConstraint, Tight, arithmetic);
                for (int i = nodes.Length - 2; i >= 0; i--)
                    nodes[i].ConstraintTo(nodes[i + 1].Position, distanceConstraint, Tight, arithmetic);
            }
            else
            {
                if (startAttached)
                {
                    for (int i = 1; i < nodes.Length - (endAttached || lockNodes ? 1 : 0); i++)
                        nodes[i].ConstraintTo(nodes[i - 1].Position, distanceConstraint, Tight, arithmetic);
                }

                if (endAttached)
                {
                    for (int i = nodes.Length - 2; i >= (startAttached || lockNodes ? 1 : 0); i--)
                        nodes[i].ConstraintTo(nodes[i + 1].Position, distanceConstraint, Tight, arithmetic);
                }
            }
        }

        private void ShakeImpulse()
        {
            for (int i = attachedStartGetter is not null ? 1 : 0;
                 i < nodes.Length - (attachedEndGetter is not null ? 1 : 0);
                 i++)
            {
                nodes[i].Acceleration +=
                    Vector2Geometry.AngleToVector(SourceRandomMath.NextAngle(((ISourceRandomEnvironment)engine).Random),
                        10000f);
            }
        }
    }
}
