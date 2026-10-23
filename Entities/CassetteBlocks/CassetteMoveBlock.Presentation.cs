// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Framework;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Entities.Platforms.SwapBlocks.Motion;
using Celeste.Game.Entities.Platforms.SwapBlocks.Rendering;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.CommunalHelper.Presentation.Colors;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Dash;
using Color = Celeste.Core.Display.PackedColor;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Celeste.Simulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Graphics.Rendering;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Celeste.Mods.CommunalHelper.Physics;
using Directions = Celeste.Simulation.Gameplay.Platforms.MoveBlock.MoveBlockDirection;
using MoveBlockDebris = Celeste.Mods.CommunalHelper.Entities.Effects.MoveBlockDebris;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    public partial class CassetteMoveBlock
    {
        public override void Update()
        {
            base.Update();
            if (moveSfx is not null && moveSfx.Playing)
            {
                float num = Vector2Geometry.Angle(Vector2Geometry.AngleToVector(angle, 1f) * new Vector2(-1f, 1f));
                int num2 = (int)Math.Floor(((0f - num + ((float)Math.PI * 2f)) % ((float)Math.PI * 2f) /
                    ((float)Math.PI * 2f) * 8f) + 0.5f);
                moveSfx.Param("arrow_influence", num2 + 1);
            }

            flash = Approach(flash, 0f, Engine.DeltaTime * 5f);
        }

        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = Position;
            Position += Shake;
            base.Render(context);
            float num = flash * 4f;
            SourcePrimitiveDrawing.Rect(context, this, X - num, Y - num, Width + (num * 2f), Height + (num * 2f),
                Color.White * flash);
            Position = position;
        }

        public override void HandleUpdateVisualState()
        {
            base.HandleUpdateVisualState();
            bool crossVisible = groupable.State == GroupableMoveBlock.MovementState.Breaking;
            bool shouldShowHighlight = groupable.Group is not null;
            arrow.Visible &= !crossVisible;
            arrowPressed.Visible &= !crossVisible;
            arrowHighlight.Visible &= !crossVisible && shouldShowHighlight;
            arrowHighlightPressed.Visible &= !crossVisible && shouldShowHighlight;
            cross.Visible &= crossVisible;
            crossPressed.Visible &= crossVisible;
            crossHighlight.Visible &= crossVisible && shouldShowHighlight;
            crossHighlightPressed.Visible &= crossVisible && shouldShowHighlight;

            if (shouldShowHighlight)
            {
                Color highlightColor = groupable.HighlightColor();
                arrowHighlight.Color = arrowHighlightPressed.Color =
                    crossHighlight.Color = crossHighlightPressed.Color = highlightColor;
            }
        }
    }
}
