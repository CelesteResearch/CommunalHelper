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
        public override void Update()
        {
            base.Update();
            if (canSteer)
            {
                bool flag = (direction == MoveBlockDirection.Up || direction == MoveBlockDirection.Down) &&
                            CollideCheck(playerTrackerKey, Position + new Vector2(-1f, 0f));
                bool flag2 = (direction == MoveBlockDirection.Up || direction == MoveBlockDirection.Down) &&
                             CollideCheck(playerTrackerKey, Position + new Vector2(1f, 0f));
                bool flag3 = (direction == MoveBlockDirection.Left || direction == MoveBlockDirection.Right) &&
                             CollideCheck(playerTrackerKey, Position + new Vector2(0f, -1f));
                foreach (Image item in topButton)
                {
                    item.Y = flag3 ? 2 : 0;
                }

                foreach (Image item2 in leftButton)
                {
                    item2.X = flag ? 2 : 0;
                }

                foreach (Image item3 in rightButton)
                {
                    item3.X = Width + (flag2 ? (-2) : 0);
                }

                if ((flag && !leftPressed) || (flag3 && !topPressed) || (flag2 && !rightPressed))
                {
                    SpatialAudio.Play("event:/game/04_cliffside/arrowblock_side_depress", Position);
                }

                if ((!flag && leftPressed) || (!flag3 && topPressed) || (!flag2 && rightPressed))
                {
                    SpatialAudio.Play("event:/game/04_cliffside/arrowblock_side_release", Position);
                }

                leftPressed = flag;
                rightPressed = flag2;
                topPressed = flag3;
            }

            if (moveSfx is not null && moveSfx.Playing)
            {
                float num = Vector2Geometry.Angle(Vector2Geometry.AngleToVector(angle, 1f) * new Vector2(-1f, 1f));
                int num2 = (int)Math.Floor(((0f - num + ((float)Math.PI * 2f)) % ((float)Math.PI * 2f) /
                    ((float)Math.PI * 2f) * 8f) + 0.5f);
                moveSfx.Param("arrow_influence", num2 + 1);
            }

            flash = Approach(flash, 0f, Engine.DeltaTime * 5f);
            UpdateColors();
        }

        public override void OnStaticMoverTrigger(StaticMover sm)
        {
            triggered = true;
        }

        public override void MoveHExact(int move)
        {
            if (noSquish is not null && ((move < 0 && noSquish.X < X) || (move > 0 && noSquish.X > X)))
            {
                while (move != 0 && noSquish.CollideCheck(solidTrackerKey, noSquish.Position + (Vector2.UnitX * move)))
                {
                    move -= Math.Sign(move);
                }
            }

            base.MoveHExact(move);
        }

        public override void MoveVExact(int move)
        {
            if (noSquish is not null && move < 0 && noSquish.Y <= Y)
            {
                while (move != 0 && noSquish.CollideCheck(solidTrackerKey, noSquish.Position + (Vector2.UnitY * move)))
                {
                    move -= Math.Sign(move);
                }
            }

            base.MoveVExact(move);
        }

        private bool MoveCheck(Vector2 speed)
        {
            if (speed.X != 0f)
            {
                if (!IsNoCollide || CollideCheck(dreamTrackerKey, Position + new Vector2(speed.X, 0f)))
                {
                    if (MoveHCollideSolids(speed.X, thruDashBlocks: false))
                    {
                        for (int i = 1; i <= 3; i++)
                        {
                            for (int num = 1; num >= -1; num -= 2)
                            {
                                Vector2 value = new(Math.Sign(speed.X), i * num);
                                if (!CollideCheck(solidTrackerKey, Position + value))
                                {
                                    MoveVExact(i * num);
                                    MoveHExact(Math.Sign(speed.X));
                                    return false;
                                }
                            }
                        }

                        return true;
                    }
                }
                else
                {
                    MoveH(speed.X);
                    return false;
                }
            }

            if (speed.Y != 0f)
            {
                if (!IsNoCollide || CollideCheck(dreamTrackerKey, Position + new Vector2(0f, speed.Y)))
                {
                    if (MoveVCollideSolids(speed.Y, thruDashBlocks: false))
                    {
                        for (int j = 1; j <= 3; j++)
                        {
                            for (int num2 = 1; num2 >= -1; num2 -= 2)
                            {
                                Vector2 value2 = new(j * num2, Math.Sign(speed.Y));
                                if (!CollideCheck(solidTrackerKey, Position + value2))
                                {
                                    MoveHExact(j * num2);
                                    MoveVExact(Math.Sign(speed.Y));
                                    return false;
                                }
                            }
                        }

                        return true;
                    }
                }
                else
                {
                    MoveV(speed.Y);
                    return false;
                }
            }

            return false;
        }
    }
}
