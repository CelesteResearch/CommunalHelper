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
        private void ActivateParticles()
        {
            bool left = !CollideCheck(playerTrackerKey, Position - Vector2.UnitX);
            bool right = !CollideCheck(playerTrackerKey, Position + Vector2.UnitX);
            bool top = !CollideCheck(playerTrackerKey, Position - Vector2.UnitY);
            if (left)
            {
                Level.ParticlesBG.Emit(P_Activate, (int)(Height / 2f), CenterLeft, Vector2.UnitY * (Height - 4f) * 0.5f,
                    (float)Math.PI);
            }

            if (right)
            {
                Level.ParticlesBG.Emit(P_Activate, (int)(Height / 2f), CenterRight,
                    Vector2.UnitY * (Height - 4f) * 0.5f,
                    0f);
            }

            if (top)
            {
                Level.ParticlesBG.Emit(P_Activate, (int)(Width / 2f), TopCenter, Vector2.UnitX * (Width - 4f) * 0.5f,
                    -(float)Math.PI / 2f);
            }

            Level.ParticlesBG.Emit(P_Activate, (int)(Width / 2f), BottomCenter, Vector2.UnitX * (Width - 4f) * 0.5f,
                (float)Math.PI / 2f);
        }

        private void BreakParticles()
        {
            Vector2 center = Center;
            ParticleType particle = Collidable ? P_Break : P_BreakPressed;
            for (int i = 0; i < Width; i += 4)
            {
                for (int j = 0; j < Height; j += 4)
                {
                    Vector2 vector = Position + new Vector2(2 + i, 2 + j);
                    Level.Particles.Emit(particle, 1, vector, Vector2.One * 2f, Vector2Geometry.Angle(vector - center));
                }
            }
        }

        private void MoveParticles()
        {
            Vector2 position;
            Vector2 positionRange;
            float num;
            float num2;
            if (Direction == Directions.Right)
            {
                position = CenterLeft + Vector2.UnitX;
                positionRange = Vector2.UnitY * (Height - 4f);
                num = (float)Math.PI;
                num2 = Height / 32f;
            }
            else if (Direction == Directions.Left)
            {
                position = CenterRight;
                positionRange = Vector2.UnitY * (Height - 4f);
                num = 0f;
                num2 = Height / 32f;
            }
            else if (Direction == Directions.Down)
            {
                position = TopCenter + Vector2.UnitY;
                positionRange = Vector2.UnitX * (Width - 4f);
                num = -(float)Math.PI / 2f;
                num2 = Width / 32f;
            }
            else
            {
                position = BottomCenter;
                positionRange = Vector2.UnitX * (Width - 4f);
                num = (float)Math.PI / 2f;
                num2 = Width / 32f;
            }

            particleRemainder += num2;
            int num3 = (int)particleRemainder;
            particleRemainder -= num3;
            positionRange *= 0.5f;
            if (num3 > 0)
            {
                Level.ParticlesBG.Emit(Collidable ? P_Move : P_MovePressed, num3, position, positionRange, num);
            }
        }

        private void ScrapeParticles(Vector2 dir)
        {
            if (Collidable)
            {
                Collidable = false;
                if (dir.X != 0f)
                {
                    float x = (!(dir.X > 0f)) ? (Left - 1f) : Right;
                    for (int i = 0; i < Height; i += 8)
                    {
                        Vector2 vector = new(x, Top + 4f + i);
                        if (SolidCollisionExclusions.CheckPoint(Scene, ignores, vector))
                        {
                            Level.ParticlesFG.Emit(((IOriginalGameplayProcess)Engine).ParticleTypes.ZipMover.P_Scrape,
                                vector);
                        }
                    }
                }
                else
                {
                    float y = (!(dir.Y > 0f)) ? (Top - 1f) : Bottom;
                    for (int j = 0; j < Width; j += 8)
                    {
                        Vector2 vector2 = new(Left + 4f + j, y);
                        if (SolidCollisionExclusions.CheckPoint(Scene, ignores, vector2))
                        {
                            Level.ParticlesFG.Emit(((IOriginalGameplayProcess)Engine).ParticleTypes.ZipMover.P_Scrape,
                                vector2);
                        }
                    }
                }

                Collidable = true;
            }
        }
    }
}
