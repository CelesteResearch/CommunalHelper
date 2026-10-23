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
        private IEnumerator Controller(ControllerEntry entry = ControllerEntry.Normal)
        {
            while (true)
            {
                float crashTimer = 0f, crashResetTimer = 0f, crashStartShakingTimer = 0f;
                if (entry == ControllerEntry.AfterBreakShake)
                {
                    entry = ControllerEntry.Normal;
                    goto AfterBreakShake;
                }

                if (entry == ControllerEntry.AfterMovementYield)
                {
                    entry = ControllerEntry.Normal;
                    goto MovementLoop;
                }

                triggered = false;
                groupable.State = GroupableMoveBlock.MovementState.Idling;
                while (!triggered && !HasPlayerRider() && !groupable.GroupTriggerSignal)
                    yield return null;

                yield return new SourceSwapImmediately(groupable.SyncGroupTriggers());

                ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                    "event:/game/04_cliffside/arrowblock_activate",
                    Position);
                groupable.State = GroupableMoveBlock.MovementState.Moving;
                StartShaking(0.2f);
                ActivateParticles();
                yield return 0.2f;

                targetSpeed = moveSpeed;
                moveSfx.Play("event:/game/04_cliffside/arrowblock_move");
                moveSfx.Param("arrow_stop", 0f);
                StopPlayerRunIntoAnimation = false;
                crashTimer = crashTime;
                crashResetTimer = CrashResetTime;
                crashStartShakingTimer = CrashStartShakingTime;
                MovementLoop:
                while (true)
                {
                    if (OnInterval(0.02f))
                    {
                        MoveParticles();
                    }

                    speed = Approach(speed, targetSpeed, Accel * Engine.DeltaTime);
                    angle = Approach(angle, targetAngle, SteerSpeed * Engine.DeltaTime);
                    Vector2 move = Vector2Geometry.AngleToVector(angle, speed) * Engine.DeltaTime;
                    bool hit;
                    if (Direction is Directions.Right or Directions.Left)
                    {
                        hit = MoveCheck(new Vector2(move.X, 0));
                        noSquish = CurrentPlayer;
                        SolidCollisionExclusions.MoveVertical(this, Engine, ignores, move.Y);
                        noSquish = null;
                        if (OnInterval(0.03f))
                        {
                            if (move.Y > 0f)
                            {
                                ScrapeParticles(Vector2.UnitY);
                            }
                            else if (move.Y < 0f)
                            {
                                ScrapeParticles(-Vector2.UnitY);
                            }
                        }
                    }
                    else
                    {
                        hit = MoveCheck(new Vector2(0, move.Y));
                        noSquish = CurrentPlayer;
                        SolidCollisionExclusions.MoveHorizontal(this, Engine, ignores, move.X);
                        noSquish = null;
                        if (OnInterval(0.03f))
                        {
                            if (move.X > 0f)
                            {
                                ScrapeParticles(Vector2.UnitX);
                            }
                            else if (move.X < 0f)
                            {
                                ScrapeParticles(-Vector2.UnitX);
                            }
                        }

                        if (Direction == Directions.Down && Top > Level.Bounds.Bottom + 32)
                        {
                            hit = true;
                        }
                    }

                    if (hit)
                    {
                        moveSfx.Param("arrow_stop", crashTimer > 0.15f ? 0.5f : 1f);
                        crashResetTimer = CrashResetTime;
                        if (crashStartShakingTimer < 0f && shakeOnCollision)
                            StartShaking();
                        if (!(crashTimer > 0f))
                        {
                            break;
                        }

                        crashTimer -= Engine.DeltaTime;
                        crashStartShakingTimer -= Engine.DeltaTime;
                    }
                    else
                    {
                        moveSfx.Param("arrow_stop", 0f);
                        if (crashResetTimer > 0f)
                        {
                            crashResetTimer -= Engine.DeltaTime;
                        }
                        else
                        {
                            StopShaking();
                            crashTimer = crashTime;
                            crashStartShakingTimer = CrashStartShakingTime;
                        }
                    }

                    OriginalLevelSceneRoot level = Scene as OriginalLevelSceneRoot;
                    if (Left < level.Bounds.Left || Top < level.Bounds.Top || Right > level.Bounds.Right)
                    {
                        break;
                    }

                    yield return null;
                }

                ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play("event:/game/04_cliffside/arrowblock_break",
                    Position);
                moveSfx.Stop();
                groupable.State = GroupableMoveBlock.MovementState.Breaking;
                speed = targetSpeed = 0f;
                angle = targetAngle = homeAngle;
                StartShaking(0.2f);
                StopPlayerRunIntoAnimation = true;
                yield return 0.2f;

                AfterBreakShake:
                BreakParticles();
                redirectable.ResetBlock();
                List<MoveBlockDebris> debris = new();
                if (!noDebris)
                {
                    for (int x = 0; x < Width; x += 8)
                    {
                        for (int y = 0; y < Height; y += 8)
                        {
                            Vector2 offset = new(x + 4f, y + 4f);

                            MoveBlockDebris d = Engine.EntityPools.Create<MoveBlockDebris>().Init(Position + offset,
                                Center,
                                startPosition + offset, spr => { spr.Color = Activated ? color : pressedColor; });
                            d.Sprite.Texture = RandomSelection.Choose(SourceWorld.GameplayRandom,
                                GameAtlas.GetAtlasSubtextures(SuffixFromSpritePathOrDefault("/debris",
                                    "objects/CommunalHelper/cassetteMoveBlock/debris")));
                            debris.Add(d);
                            ((ISourceScene)Scene).SourceScene.QueueSourceEntity(d);
                        }
                    }
                }

                Vector2 newPosition = startPosition + BlockOffset;
                SetDisabledStaticMoversVisibility(false);
                SetStaticMoversVisible(false);
                MoveStaticMovers(newPosition - Position);
                Position = newPosition;
                Visible = false;
                Present = false;

                float waitTime = Clamp(regenTime - 0.8f, 0, float.MaxValue);
                float debrisShakeTime = Clamp(regenTime - 0.6f, 0, 0.2f);
                float debrisMoveTime = Clamp(regenTime, 0, 0.6f);

                yield return waitTime;

                yield return new SourceSwapImmediately(groupable.WaitForRespawn());

                foreach (MoveBlockDebris d in debris)
                    d.StopMoving();
                while (CollideCheck(actorTrackerKey, Position) ||
                       SolidCollisionExclusions.Check(this, ignores, Position))
                    yield return null;

                Present = true;
                ISourceAudioEventReference sound = ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                    "event:/game/04_cliffside/arrowblock_reform_begin", debris.FirstOrDefault()?.Position ?? Center);
                SimulationCoroutine component;
                SimulationCoroutine routine =
                    component = new SimulationCoroutine(Engine, SoundFollowsDebrisCenter(sound, debris));
                OwnedComponents.Add(component);
                foreach (MoveBlockDebris d in debris)
                    d.StartShaking();
                yield return debrisShakeTime;

                foreach (MoveBlockDebris d in debris)
                    d.ReturnHome(debrisMoveTime + 0.05f);
                yield return debrisMoveTime;

                routine.RemoveSelf();
                foreach (MoveBlockDebris d in debris)
                    d.RemoveSelf();

                groupable.WaitingForRespawn = false;
                ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                    "event:/game/04_cliffside/arrowblock_reappear",
                    Position);
                Visible = true;
                SetDisabledStaticMoversVisibility(true);
                SetStaticMoversVisible(true);

                if (Collidable)
                    EnableStaticMovers();
                else
                {
                    // would inexplicably appear turned on
                    DisableStaticMovers();
                }

                speed = targetSpeed = 0f;
                angle = targetAngle = homeAngle;
                noSquish = null;
                flash = 1f;
            }
        }

        private IEnumerator SoundFollowsDebrisCenter(ISourceAudioEventReference instance, List<MoveBlockDebris> debris)
        {
            while (true && debris.Count > 0)
            {
                SourceAudioPlaybackState state = instance.PlaybackState;
                if (state == SourceAudioPlaybackState.Stopped)
                {
                    break;
                }

                Vector2 center = Vector2.Zero;
                foreach (MoveBlockDebris d in debris)
                {
                    center += d.Position;
                }

                center /= debris.Count;
                ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Position(instance, center);
                yield return null;
            }
        }
    }
}
