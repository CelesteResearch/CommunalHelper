// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Enemies.Seeker;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Everest.Modules.Runtime;
using Celeste.Mods.CommunalHelper.Modules;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedMoveBlock
    {
        protected class Border : SceneEntityState
        {
            public ConnectedMoveBlock Parent;
            private static Vector2 offset = new(1, 1);

            public Border(ConnectedMoveBlock parent)
                : base(((Celeste.Core.Components.ISourceEntitySourceDataEnvironment)parent.SourceWorld.EngineContext)
                    .EntitySourceDataRegistration)
            {
                Parent = parent;
                Depth = parent.BGRenderer.Depth + 1;
            }

            public override void Update()
            {
                if (Parent.Scene != Scene)
                    RemoveSelf();

                base.Update();
            }

            public override void Render(ISourceRenderContext context)
            {
                foreach (Hitbox hitbox in Parent.AllColliders)
                {
                    if (Parent.outline)
                        SourcePrimitiveDrawing.Rect(context, this,
                            hitbox.Position + Parent.Position + Parent.Shake - offset, hitbox.Width + 2f,
                            hitbox.Height + 2f, Color.Black);

                    float num = Parent.flash * 4f;
                    if (Parent.flash > 0f)
                        SourcePrimitiveDrawing.Rect(context, this,
                            hitbox.Position + Parent.Position - new Vector2(num, num), hitbox.Width + (2f * num),
                            hitbox.Height + (2f * num), Color.White * Parent.flash);
                }
            }
        }
    }
}
