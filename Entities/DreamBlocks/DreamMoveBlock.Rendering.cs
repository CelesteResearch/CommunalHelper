// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celeste.Graphics.Rendering.Drawing;
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
using System.Runtime.CompilerServices;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Mathematics.Easing;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Mods.CommunalHelper.Entities.Effects;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Core.Randomness;
using Player = Celeste.Simulation.RoomPlayerSession;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using StaticMover = Celeste.Simulation.Gameplay.Platforms.StaticMovers.SourceStaticMoverState;
using EventInstance = Celeste.Core.Audio.ISourceAudioEventReference;
using Coroutine = Celeste.Core.Scheduling.SimulationCoroutine;
using LightOcclude = Celeste.Simulation.Presentation.Lighting.LightOccludeState;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    public partial class DreamMoveBlock
    {
        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = Position;
            Position += Shake;

            var oldColor = Celeste.Mods.CommunalHelper.Runtime.DreamBlocks.DreamLineColorFields.Get();
            Celeste.Mods.CommunalHelper.Runtime.DreamBlocks.DreamLineColorFields.Set(wobbleLineColor);

            base.Render(context);

            if (groupable.State != GroupableMoveBlock.MovementState.Breaking && canSteer)
            {
                foreach (Image img in leftButton) img.Render(context);
                foreach (Image img in rightButton) img.Render(context);
                foreach (Image img in topButton) img.Render(context);
                foreach (Image img in body) img.Render(context);
            }

            int value = (int)Math.Floor(((0f - angle + ((float)Math.PI * 2f)) % ((float)Math.PI * 2f) /
                ((float)Math.PI * 2f) * 8f) + 0.5f);
            MTexture currentTex = groupable.State != GroupableMoveBlock.MovementState.Breaking
                ? arrows[Clamp(value, 0, 7)]
                : Atlas["objects/CommunalHelper/dreamMoveBlock/x"];
            SourceAtlasDrawing.DrawCentered(context, this, currentTex, Center + SourceShake,
                groupable.HighlightColor(currentArrowColor));

            float num = flash * 4f;
            SourcePrimitiveDrawing.Rect(context, this, X - num, Y - num, Width + (num * 2f), Height + (num * 2f),
                Color.White * flash);
            Position = position;

            Celeste.Mods.CommunalHelper.Runtime.DreamBlocks.DreamLineColorFields.Set(oldColor);
        }

        /// <summary>
        /// This method is called to update the buttons, arrow and body colors
        /// </summary>
        private void UpdateColors()
        {
            if (direction == MoveBlockDirection.Left || direction == MoveBlockDirection.Right)
            {
                if (canSteer)
                {
                    topWobble = false;
                }
            }
            else if (direction == MoveBlockDirection.Up || direction == MoveBlockDirection.Down)
            {
                if (canSteer)
                {
                    leftWobble = false;
                    rightWobble = false;
                }
            }

            if (groupable.State == GroupableMoveBlock.MovementState.Idling)
            {
                fillColor = Lerp(fillColor, idleButtonsColor, 10f * Engine.DeltaTime);
                wobbleLineColor = idleWobbleLinesColor;
            }
            else if (groupable.State == GroupableMoveBlock.MovementState.Moving)
            {
                fillColor = Lerp(fillColor, movingButtonsColor, 10f * Engine.DeltaTime);
                wobbleLineColor = movingWobbleLinesColor;
            }
            else if (groupable.State == GroupableMoveBlock.MovementState.Breaking && canSteer)
            {
                topWobble = true;
                bottomWobble = true;
                leftWobble = true;
                rightWobble = true;
                wobbleLineColor = breakingWobbleLinesColor;
            }

            if (canSteer)
            {
                foreach (Image img in topButton) img.Color = fillColor;
                foreach (Image img in leftButton) img.Color = fillColor;
                foreach (Image img in rightButton) img.Color = fillColor;
                foreach (Image img in body) img.Color = fillColor;
            }

            Color value = groupable.State switch
            {
                GroupableMoveBlock.MovementState.Moving => movingArrowColor,
                GroupableMoveBlock.MovementState.Breaking => breakingCrossColor,
                _ => idleArrowColor,
            };
            currentArrowColor = Lerp(currentArrowColor, value, 10f * Engine.DeltaTime);
        }

        private void AddImage(MTexture tex, Vector2 position, float rotation, Vector2 scale, List<Image> addTo)
        {
            Image image = new(tex)
            {
                Position = position + new Vector2(4f, 4f),
                Rotation = rotation,
                Scale = scale
            };
            image.CenterOrigin();
            Add(image);
            addTo?.Add(image);
        }
    }
}
