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
        private IEnumerator Controller(ControllerEntry entry = ControllerEntry.Normal)
        {
            while (true)
            {
                float crashTimer = 0f, crashResetTimer = 0f, crashStartShakingTimer = 0f, noSteerTimer = 0f;
                if (entry != ControllerEntry.AfterBreakShake)
                {
                    if (entry == ControllerEntry.Normal)
                    {
                        triggered = false;
                        groupable.State = GroupableMoveBlock.MovementState.Idling;
                        while (!triggered && !groupable.GroupTriggerSignal && !HasPlayerRider())
                            yield return null;

                        yield return new SourceSwapImmediately(groupable.SyncGroupTriggers());

                        SpatialAudio.Play(
                            PlayerHasDreamDash
                                ? "event:/CommunalHelperEvents/game/dreamMoveBlock/dream_move_block_activate"
                                : "event:/game/04_cliffside/arrowblock_activate", Position);
                        groupable.State = GroupableMoveBlock.MovementState.Moving;
                        StartShaking(0.2f);
                        ActivateParticles();
                        yield return 0.2f;


                        targetSpeed = moveSpeed;
                        moveSfx.Play("event:/CommunalHelperEvents/game/redirectMoveBlock/arrowblock_move");
                        moveSfx.Param("arrow_stop", 0f);
                        StopPlayerRunIntoAnimation = false;

                        crashTimer = crashTime;
                        crashResetTimer = CrashResetTime;
                        crashStartShakingTimer = CrashStartShakingTime;
                        noSteerTimer = 0.2f;
                    }

                    while (true)
                    {
                        if (entry == ControllerEntry.AfterMovementYield)
                        {
                            entry = ControllerEntry.Normal;
                            continue;
                        }

                        if (canSteer)
                        {
                            targetAngle = homeAngle;
                            bool flag = (direction != MoveBlockDirection.Right && direction != 0)
                                ? HasPlayerClimbing()
                                : HasPlayerOnTop();
                            if (flag && noSteerTimer > 0f)
                            {
                                noSteerTimer -= Engine.DeltaTime;
                            }

                            if (flag)
                            {
                                if (noSteerTimer <= 0f)
                                {
                                    if (direction == MoveBlockDirection.Right || direction == MoveBlockDirection.Left)
                                        targetAngle = homeAngle + (float)Math.PI / 4f * angleSteerSign *
                                            ((IOriginalGameInputProcessEnvironment)Engine).InputSource.MoveY.Value;
                                    else
                                        targetAngle = homeAngle + (float)Math.PI / 4f * angleSteerSign *
                                            ((IOriginalGameInputProcessEnvironment)Engine).InputSource.MoveX.Value;
                                }
                            }
                            else
                            {
                                noSteerTimer = 0.2f;
                            }
                        }

                        if (OnInterval(0.02f) && Collidable)
                        {
                            MoveParticles();
                        }

                        speed = Approach(speed, targetSpeed, Accel * Engine.DeltaTime);
                        angle = Approach(angle, targetAngle, SteerSpeed * Engine.DeltaTime);
                        Vector2 move = Vector2Geometry.AngleToVector(angle, speed) * Engine.DeltaTime;
                        bool hit;
                        if (direction is MoveBlockDirection.Right or MoveBlockDirection.Left)
                        {
                            hit = MoveCheck(new Vector2(move.X, 0f));
                            noSquish = CurrentPlayer;

                            if (canSteer || noCollideSteer)
                                MoveCheck(new Vector2(0f, move.Y));
                            else
                                MoveVCollideSolids(move.Y, thruDashBlocks: false);

                            noSquish = null;
                            if (OnInterval(0.03f))
                            {
                                if (move.Y > 0f)
                                    ScrapeParticles(Vector2.UnitY);
                                else if (move.Y < 0f)
                                    ScrapeParticles(-Vector2.UnitY);
                            }
                        }
                        else
                        {
                            hit = MoveCheck(new Vector2(0f, move.Y));
                            noSquish = CurrentPlayer;

                            if (canSteer || noCollideSteer)
                                MoveCheck(new Vector2(move.X, 0f));
                            else
                                MoveHCollideSolids(move.X, thruDashBlocks: false);

                            noSquish = null;
                            if (OnInterval(0.03f))
                            {
                                if (move.X > 0f)
                                    ScrapeParticles(Vector2.UnitX);
                                else if (move.X < 0f)
                                    ScrapeParticles(-Vector2.UnitX);
                            }

                            if (direction == MoveBlockDirection.Down && Top > Level.Bounds.Bottom + 32)
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
                            if (!(crashTimer > 0f) && !shattering)
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

                        OriginalLevelSceneRoot level = Level;
                        if (Left < level.Bounds.Left || Top < level.Bounds.Top || Right > level.Bounds.Right)
                            break;

                        yield return null;
                    }


                    SpatialAudio.Play(
                        PlayerHasDreamDash
                            ? "event:/CommunalHelperEvents/game/dreamMoveBlock/dream_move_block_break"
                            : "event:/game/04_cliffside/arrowblock_break", Position);
                    moveSfx.Stop();
                    groupable.State = GroupableMoveBlock.MovementState.Breaking;
                    speed = targetSpeed = 0f;
                    angle = targetAngle = homeAngle;
                    StartShaking(0.2f);
                    StopPlayerRunIntoAnimation = true;
                    yield return 0.2f;
                }

                entry = ControllerEntry.Normal;
                BreakParticles();
                ((MoveBlockRedirectable)Get<Redirectable>())?.ResetBlock();
                List<MoveBlockDebris> debris = new();
                if (!noDebris)
                {
                    for (int x = 0; x < Width; x += 8)
                    {
                        for (int y = 0; y < Height; y += 8)
                        {
                            Vector2 offset = new(x + 4f, y + 4f);
                            MTexture texture = RandomSelection.Choose(RandomSource,
                                Atlas.GetAtlasSubtextures("objects/CommunalHelper/dreamMoveBlock/debris"));
                            MTexture altTexture = Atlas[texture.AtlasPath.Replace("debris", "disabledDebris")];
                            MoveBlockDebris d = Engine.EntityPools.Create<MoveBlockDebris>()
                                .Init(Position + offset, Center, startPosition + offset,
                                    spr => { spr.Texture = PlayerHasDreamDash ? texture : altTexture; });
                            debris.Add(d);
                            ((ISourceScene)Scene).SourceScene.QueueSourceEntity(d);
                        }
                    }
                }

                MoveStaticMovers(startPosition - Position);
                DisableStaticMovers();
                Position = startPosition;
                Visible = Collidable = false;


                float waitTime = Clamp(regenTime - 0.8f, 0, float.MaxValue);
                float debrisShakeTime = Clamp(regenTime - 0.6f, 0, 0.2f);
                float debrisMoveTime = Clamp(regenTime, 0, 0.6f);

                yield return waitTime;

                yield return new SourceSwapImmediately(groupable.WaitForRespawn());

                foreach (MoveBlockDebris d in debris)
                {
                    d.StopMoving();
                }

                if (oneUseBroken)
                {
                    OneUseDestroy();
                    yield break;
                }

                while (CollideCheck(actorTrackerKey) ||
                       (IsNoCollide ? CollideCheck(dreamTrackerKey) : CollideCheck(solidTrackerKey)))
                {
                    yield return null;
                }


                Collidable = true;
                EventInstance sound = SpatialAudio.Play("event:/game/04_cliffside/arrowblock_reform_begin",
                    debris.FirstOrDefault()?.Position ?? Center);
                Coroutine soundFollower = new(Engine, SoundFollowsDebrisCenter(sound, debris));
                Add(soundFollower);
                foreach (MoveBlockDebris d in debris)
                {
                    d.StartShaking();
                }

                yield return debrisShakeTime;


                foreach (MoveBlockDebris d in debris)
                {
                    d.ReturnHome(debrisMoveTime + 0.05f);
                }

                yield return debrisMoveTime;


                soundFollower.RemoveSelf();
                foreach (MoveBlockDebris d in debris)
                {
                    d.RemoveSelf();
                }

                groupable.WaitingForRespawn = false;
                SpatialAudio.Play(
                    PlayerHasDreamDash
                        ? "event:/CommunalHelperEvents/game/dreamMoveBlock/dream_move_block_reappear"
                        : "event:/game/04_cliffside/arrowblock_reappear", Position);
                Visible = true;
                EnableStaticMovers();
                speed = targetSpeed = 0f;
                angle = targetAngle = homeAngle;
                noSquish = null;
                fillColor = idleButtonsColor;
                UpdateColors();
                flash = 1f;
            }
        }

        private IEnumerator SoundFollowsDebrisCenter(EventInstance instance, List<MoveBlockDebris> debris)
        {
            while (true && debris.Count > 0)
            {
                var state = instance.PlaybackState;
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
                SpatialAudio.Position(instance, center);
                yield return null;
            }
        }
    }
}
