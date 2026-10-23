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
        protected void ActivateParticles()
        {
            foreach (Hitbox hitbox in Colliders)
            {
                bool left = !CollideCheck(typeof(RoomPlayerSession).FullName, Position - Vector2.UnitX);
                bool right = !CollideCheck(typeof(RoomPlayerSession).FullName, Position + Vector2.UnitX);
                bool top = !CollideCheck(typeof(RoomPlayerSession).FullName, Position - Vector2.UnitY);

                if (left)
                {
                    Level.ParticlesBG.Emit(
                        ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.MoveBlock.P_Activate,
                        (int)(hitbox.Height / 2f), Position + hitbox.CenterLeft,
                        Vector2.UnitY * (hitbox.Height - 4f) * 0.5f, (float)Math.PI);
                }

                if (right)
                {
                    Level.ParticlesBG.Emit(
                        ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.MoveBlock.P_Activate,
                        (int)(hitbox.Height / 2f), Position + hitbox.CenterRight,
                        Vector2.UnitY * (hitbox.Height - 4f) * 0.5f, 0f);
                }

                if (top)
                {
                    Level.ParticlesBG.Emit(
                        ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.MoveBlock.P_Activate,
                        (int)(hitbox.Width / 2f), Position + hitbox.TopCenter,
                        Vector2.UnitX * (hitbox.Width - 4f) * 0.5f,
                        -(float)Math.PI / 2f);
                }

                Level.ParticlesBG.Emit(
                    ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.MoveBlock.P_Activate,
                    (int)(hitbox.Width / 2f), Position + hitbox.BottomCenter,
                    Vector2.UnitX * (hitbox.Width - 4f) * 0.5f,
                    (float)Math.PI / 2f);
            }
        }

        protected void BreakParticles()
        {
            foreach (Hitbox hitbox in Colliders)
            {
                Vector2 center = Position + hitbox.Center;
                for (int i = 0; i < hitbox.Width; i += 4)
                {
                    for (int j = 0; j < hitbox.Height; j += 4)
                    {
                        Vector2 vector = Position + hitbox.Position + new Vector2(2 + i, 2 + j);
                        Level.Particles.Emit(
                            ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.MoveBlock.P_Break, 1,
                            vector, Vector2.One * 2f, Vector2Geometry.Angle(vector - center));
                    }
                }
            }
        }

        protected void MoveParticles()
        {
            foreach (Hitbox hitbox in Colliders)
            {
                Vector2 position;
                Vector2 positionRange;
                float num;
                float num2;
                if (Direction == MoveBlockDirection.Right)
                {
                    position = hitbox.CenterLeft + Vector2.UnitX;
                    positionRange = Vector2.UnitY * (hitbox.Height - 4f);
                    num = (float)Math.PI;
                    num2 = hitbox.Height / 32f;
                }
                else if (Direction == MoveBlockDirection.Left)
                {
                    position = hitbox.CenterRight;
                    positionRange = Vector2.UnitY * (hitbox.Height - 4f);
                    num = 0f;
                    num2 = hitbox.Height / 32f;
                }
                else if (Direction == MoveBlockDirection.Down)
                {
                    position = hitbox.TopCenter + Vector2.UnitY;
                    positionRange = Vector2.UnitX * (hitbox.Width - 4f);
                    num = -(float)Math.PI / 2f;
                    num2 = hitbox.Width / 32f;
                }
                else
                {
                    position = hitbox.BottomCenter;
                    positionRange = Vector2.UnitX * (hitbox.Width - 4f);
                    num = (float)Math.PI / 2f;
                    num2 = hitbox.Width / 32f;
                }

                particleRemainder += num2;
                int num3 = (int)particleRemainder;
                particleRemainder -= num3;
                positionRange *= 0.5f;
                if (num3 > 0)
                {
                    Level.ParticlesBG.Emit(
                        ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.MoveBlock.P_Move, num3,
                        position + Position, positionRange, num);
                }
            }
        }
    }
}
