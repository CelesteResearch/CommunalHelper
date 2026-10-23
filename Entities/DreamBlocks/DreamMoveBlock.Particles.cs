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
        private void ActivateParticles()
        {
            bool flag = direction == MoveBlockDirection.Down || direction == MoveBlockDirection.Up;
            bool left = (!canSteer || !flag) && !CollideCheck(playerTrackerKey, Position - Vector2.UnitX);
            bool right = (!canSteer || !flag) && !CollideCheck(playerTrackerKey, Position + Vector2.UnitX);
            bool top = (!canSteer || !flag) && !CollideCheck(playerTrackerKey, Position - Vector2.UnitY);
            if (left)
            {
                for (int i = 1; i < (Height / 2) - 1; ++i)
                {
                    ParticleType particle = dreamParticles[activateParticleIndex];
                    Vector2 position = TopLeft + (Vector2.UnitY * i * 2);
                    Level.Particles.Emit(particle, 1, position, Vector2.One, (float)Math.PI);
                    ++activateParticleIndex;
                    activateParticleIndex %= 4;
                }
            }

            if (right)
            {
                for (int i = 1; i < (Height / 2) - 1; ++i)
                {
                    ParticleType particle = dreamParticles[activateParticleIndex];
                    Vector2 position = TopRight + (Vector2.UnitY * i * 2);
                    Level.Particles.Emit(particle, 1, position, Vector2.One, 0f);
                    ++activateParticleIndex;
                    activateParticleIndex %= 4;
                }
            }

            if (top)
            {
                for (int i = 1; i < (Width / 2) - 1; ++i)
                {
                    ParticleType particle = dreamParticles[activateParticleIndex];
                    Vector2 position = TopLeft + (Vector2.UnitX * i * 2);
                    Level.Particles.Emit(particle, 1, position, Vector2.One, -(float)Math.PI / 2f);
                    ++activateParticleIndex;
                    activateParticleIndex %= 4;
                }
            }

            for (int i = 1; i < (Width / 2) - 1; ++i)
            {
                ParticleType particle = dreamParticles[activateParticleIndex];
                Vector2 position = BottomLeft + (Vector2.UnitX * i * 2);
                Level.Particles.Emit(particle, 1, position, Vector2.One, (float)Math.PI / 2f);
                ++activateParticleIndex;
                activateParticleIndex %= 4;
            }
        }

        private void BreakParticles()
        {
            Vector2 center = Center;
            for (int i = 0; i < Width; i += 4)
            {
                for (int j = 0; j < Height; j += 4)
                {
                    Vector2 vector = Position + new Vector2(2 + i, 2 + j);
                    Level.Particles.Emit(dreamParticles[breakParticleIndex], 1, vector, Vector2.One * 2f,
                        Vector2Geometry.Angle(vector - center));
                    ++breakParticleIndex;
                    breakParticleIndex %= 4;
                }
            }
        }

        private void MoveParticles()
        {
            Vector2 position;
            Vector2 positionRange;
            float dir;
            float num2;
            switch (direction)
            {
                default:
                case MoveBlockDirection.Right:
                    position = CenterLeft + Vector2.UnitX;
                    positionRange = Vector2.UnitY * (Height - 4f);
                    dir = (float)Math.PI;
                    num2 = Height / 32f;
                    break;
                case MoveBlockDirection.Left:
                    position = CenterRight;
                    positionRange = Vector2.UnitY * (Height - 4f);
                    dir = 0f;
                    num2 = Height / 32f;
                    break;
                case MoveBlockDirection.Down:

                    position = TopCenter + Vector2.UnitY;
                    positionRange = Vector2.UnitX * (Width - 4f);
                    dir = -(float)Math.PI / 2f;
                    num2 = Width / 32f;
                    break;

                case MoveBlockDirection.Up:
                    position = BottomCenter;
                    positionRange = Vector2.UnitX * (Width - 4f);
                    dir = (float)Math.PI / 2f;
                    num2 = Width / 32f;
                    break;
            }

            particleRemainder += num2;
            int amount = (int)particleRemainder;
            particleRemainder -= amount;
            positionRange *= 0.5f;
            if (amount > 0)
            {
                Level.ParticlesBG.Emit(dreamParticles[moveParticleIndex], amount, position, positionRange, dir);
                ++moveParticleIndex;
                moveParticleIndex %= 4;
            }
        }

        private void ScrapeParticles(Vector2 dir)
        {
            if (IsNoCollide)
                return;

            Collidable = false;
            if (dir.X != 0f)
            {
                float x = (!(dir.X > 0f)) ? (Left - 1f) : Right;
                for (int i = 0; i < Height; i += 8)
                {
                    Vector2 vector = new(x, Top + 4f + i);
                    if (SourceSceneCollision.CheckPoint(((ISourceScene)Scene).SourceScene.Entities, solidTrackerKey,
                            vector))
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
                    if (SourceSceneCollision.CheckPoint(((ISourceScene)Scene).SourceScene.Entities, solidTrackerKey,
                            vector2))
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
