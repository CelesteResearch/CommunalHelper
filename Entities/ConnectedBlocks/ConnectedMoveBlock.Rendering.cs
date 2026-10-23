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
        public override void Render(ISourceRenderContext context)
        {
            if (!IsGroupVisible())
                return;

            Vector2 position = Position;
            Position += Shake;

            foreach (Hitbox hitbox in Colliders)
            {
                SourcePrimitiveDrawing.Rect(context, this, hitbox.Position.X + Position.X,
                    hitbox.Position.Y + Position.Y,
                    hitbox.Width, hitbox.Height, fillColor);
            }

            base.Render(context);

            int arrowIndex =
                Clamp(
                    (int)Math.Floor(((0f - angle + ((float)Math.PI * 2f)) % ((float)Math.PI * 2f) /
                                     ((float)Math.PI * 2f) *
                                     8f) + 0.5f), 0, 7);
            MTexture arrowTex = arrows is null ? masterArrows[arrowIndex] : arrows[arrowIndex];
            MTexture xTex = x ?? masterX;
            Color centerColor = groupable.HighlightColor(fillColor);
            foreach (Hitbox hitbox in ArrowsList)
            {
                Vector2 centerPos = hitbox.Center + Position;
                if (groupable.State != GroupableMoveBlock.MovementState.Breaking)
                {
                    if (noArrowSprite) continue;
                    SourcePrimitiveDrawing.Rect(context, this, centerPos.X - arrowTex.FrameWidth / 2f,
                        centerPos.Y - arrowTex.FrameHeight / 2f, arrowTex.FrameWidth, arrowTex.FrameHeight,
                        centerColor);
                    SourceAtlasDrawing.DrawCentered(context, this, arrowTex, centerPos, Color.White);
                }
                else
                {
                    if (noBreakingSprite) continue;
                    SourcePrimitiveDrawing.Rect(context, this, centerPos.X - xTex.FrameWidth / 2f,
                        centerPos.Y - xTex.FrameHeight / 2f, xTex.FrameWidth, xTex.FrameHeight, centerColor);
                    SourceAtlasDrawing.DrawCentered(context, this, xTex, centerPos, Color.White);
                }
            }

            foreach (Image img in Tiles)
                SourcePrimitiveDrawing.Rect(context, this, img.Position + Position, 8, 8, Color.White * flash);

            Position = position;
        }
    }
}
