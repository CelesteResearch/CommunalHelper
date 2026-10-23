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
        private IEnumerator Controller(ControllerEntry entry = ControllerEntry.Normal)
        {
            // If we're waiting for flags before becoming visible, start off invisible.
            bool startInvisible = entry == ControllerEntry.Normal && AnySetEnabled(BreakerFlags) && WaitForFlags;
            if (startInvisible)
                Visible = Collidable = false;
            while (true)
            {
                bool startingBroken = false, startingByActivator = false;
                float crashTimer = 0, crashResetTimer = 0, crashStartShakingTimer = 0;
                if (entry == ControllerEntry.Normal)
                {
                    curMoveCheck = false;
                    triggered = false;
                    groupable.State = GroupableMoveBlock.MovementState.Idling;
                    while (!triggered && !startingByActivator && !startingBroken && !groupable.GroupTriggerSignal)
                    {
                        if (startInvisible && !AnySetEnabled(BreakerFlags))
                        {
                            goto Rebuild;
                        }

                        yield return null;
                        startingBroken = AnySetEnabled(BreakerFlags) && !startInvisible;
                        startingByActivator = AnySetEnabled(ActivatorFlags);
                    }

                    yield return new SourceSwapImmediately(groupable.SyncGroupTriggers());

                    SpatialAudio.Play(ActivateSoundEffect, Position);
                    groupable.State = GroupableMoveBlock.MovementState.Moving;
                    StartShaking(0.2f);
                    ActivateParticles();
                    if (!startingBroken)
                    {
                        foreach (string flag in OnActivateFlags)
                        {
                            if (flag.Length > 0)
                                if (flag.StartsWith("!"))
                                {
                                    Level.Session.SetFlag(flag.Substring(1), false);
                                }
                                else if (flag.StartsWith("~"))
                                {
                                    Level.Session.SetFlag(flag.Substring(1), !Level.Session.GetFlag(flag.Substring(1)));
                                }
                                else
                                    Level.Session.SetFlag(flag);
                        }
                    }
                    else
                        groupable.State = GroupableMoveBlock.MovementState.Breaking;

                    yield return 0.2f;
                    targetSpeed = moveSpeed;
                    moveSfx.Play(MoveSoundEffect);
                    moveSfx.Param("arrow_stop", 0f);
                    StopPlayerRunIntoAnimation = false;
                    crashTimer = crashTime;
                    crashResetTimer = CrashResetTime;
                    crashStartShakingTimer = CrashStartShakingTime;
                }

                if (entry != ControllerEntry.AfterBreakShake)
                {
                    while (true)
                    {
                        if (OnInterval(0.02f))
                        {
                            MoveParticles();
                        }

                        speed = startingBroken
                            ? 0
                            : Approach(speed, targetSpeed, 300f * SourceWorld.EngineContext.DeltaTime);
                        angle = Approach(angle, targetAngle,
                            (float)Math.PI * 16f * SourceWorld.EngineContext.DeltaTime);
                        Vector2 vec = Vector2Geometry.AngleToVector(angle, speed) * SourceWorld.EngineContext.DeltaTime;
                        bool flag2;
                        Vector2 start = Position;
                        if (Direction is MoveBlockDirection.Right or MoveBlockDirection.Left)
                        {
                            flag2 = MoveCheck(new Vector2(vec.X, 0));
                            noSquish = FirstPlayer;
                            MoveVCollideSolidsExcluding(vec.Y);
                            noSquish = null;
                        }
                        else
                        {
                            flag2 = MoveCheck(new Vector2(0, vec.Y));
                            noSquish = FirstPlayer;
                            MoveHCollideSolidsExcluding(vec.X);
                            noSquish = null;
                            if (Direction == MoveBlockDirection.Down && Top > Level.Bounds.Bottom + 32)
                            {
                                flag2 = true;
                            }
                        }

                        Vector2 move = Position - start;
                        if (OnInterval(0.03f))
                            SpawnScrapeParticlesExcluding(ignores, Math.Abs(move.X) != 0, Math.Abs(move.Y) != 0);

                        curMoveCheck = flag2;

                        if (startingBroken || AnySetEnabled(BreakerFlags))
                        {
                            moveSfx.Param("arrow_stop", crashTimer > 0.15f ? 0.5f : 1f);
                            crashResetTimer = CrashResetTime;
                            if (crashStartShakingTimer < 0f && shakeOnCollision)
                                StartShaking();
                            if (!(crashTimer > 0f))
                            {
                                break;
                            }

                            crashTimer -= SourceWorld.EngineContext.DeltaTime;
                            crashStartShakingTimer -= SourceWorld.EngineContext.DeltaTime;
                        }
                        else
                        {
                            moveSfx.Param("arrow_stop", 0f);
                            if (crashResetTimer > 0f)
                            {
                                crashResetTimer -= SourceWorld.EngineContext.DeltaTime;
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
                        {
                            break;
                        }

                        yield return null;
                    }

                    SpatialAudio.Play(BreakSoundEffect, Position);
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
                if (!redirectIsPersistent)
                    Components.Get<MoveBlockRedirectable>()?.ResetBlock();

                List<MoveBlockDebrisState> debris = new();
                if (!noDebris)
                {
                    int tWidth = (int)((GroupBoundsMax.X - GroupBoundsMin.X) / 8);
                    int tHeight = (int)((GroupBoundsMax.Y - GroupBoundsMin.Y) / 8);

                    for (int i = 0; i < tWidth; i++)
                    {
                        for (int j = 0; j < tHeight; j++)
                        {
                            if (AllGroupTiles[i, j])
                            {
                                Vector2 value = new((i * 8) + 4, (j * 8) + 4);
                                Vector2 pos = value + Position + GroupOffset;
                                MoveBlockDebrisState debris2 = SourceWorld.EngineContext.EntityPools
                                    .Create<MoveBlockDebrisState>()
                                    .Init(pos, GroupCenter, startPosition + GroupOffset + value);
                                debris2.Sprite.Texture =
                                    RandomSelection.Choose(SourceWorld.GameplayRandom, this.debris ?? masterDebris);
                                debris.Add(debris2);
                                ((ISourceScene)Scene).SourceScene.QueueSourceEntity(debris2);
                            }
                        }
                    }
                }

                MoveStaticMovers(startPosition - Position);
                DisableStaticMovers();

                bool shouldProcessBreakFlags = true;
                if (BarrierBlocksFlags)
                {
                    bool colliding = false;
                    foreach (SeekerBarrierState entity in ((ISourceScene)Scene).SourceScene.Entities
                             .GetTrackedEntities(typeof(SeekerBarrierState)).Cast<SeekerBarrierState>())
                    {
                        entity.Collidable = true;
                        bool collision = Collider.Collide(entity.Collider);
                        colliding |= collision;
                        entity.Collidable = false;
                    }

                    shouldProcessBreakFlags = !colliding;
                }

                Position = startPosition;
                Visible = Collidable = false;

                float waitTime = Clamp(regenTime - 0.8f, 0, float.MaxValue);
                float debrisShakeTime = Clamp(regenTime - 0.6f, 0, 0.2f);
                float debrisMoveTime = Clamp(regenTime, 0, 0.6f);

                if (shouldProcessBreakFlags)
                {
                    foreach (string flag in OnBreakFlags)
                    {
                        if (flag.Length > 0)
                        {
                            if (flag.StartsWith("!"))
                            {
                                Level.Session.SetFlag(flag.Substring(1), false);
                            }
                            else if (flag.StartsWith("~"))
                            {
                                Level.Session.SetFlag(flag.Substring(1), !Level.Session.GetFlag(flag.Substring(1)));
                            }
                            else
                                Level.Session.SetFlag(flag);
                        }
                    }
                }

                curMoveCheck = false;
                yield return waitTime;

                yield return new SourceSwapImmediately(groupable.WaitForRespawn());

                foreach (MoveBlockDebrisState item in debris)
                {
                    item.StopMoving();
                }

                while (CollideCheck(typeof(SourceActorEntityState).FullName, Position) ||
                       CollideSolidsExcluding(Position) || AnySetEnabled(BreakerFlags))
                {
                    yield return null;
                }

                Collidable = true;
                ISourceAudioEventReference instance =
                    SpatialAudio.Play(ReformBeginSoundEffect, debris.FirstOrDefault()?.Position ?? Center);
                SimulationCoroutine component;
                SimulationCoroutine routine = component = new SimulationCoroutine(SourceWorld.EngineContext,
                    SoundFollowsDebrisCenter(instance, debris));
                Add(component);
                foreach (MoveBlockDebrisState item2 in debris)
                {
                    item2.StartShaking();
                }

                yield return debrisShakeTime;

                foreach (MoveBlockDebrisState item3 in debris)
                {
                    item3.ReturnHome(debrisMoveTime + 0.05f);
                }

                yield return debrisMoveTime;

                routine.RemoveSelf();
                foreach (MoveBlockDebrisState item4 in debris)
                {
                    item4.RemoveSelf();
                }

                groupable.WaitingForRespawn = false;
                Rebuild:
                SpatialAudio.Play(ReappearSoundEffect, Position);
                Visible = true;
                Collidable = true;
                EnableStaticMovers();
                speed = targetSpeed = 0f;
                angle = targetAngle = homeAngle;
                noSquish = null;
                fillColor = idleBgFill;
                UpdateColors();
                flash = 1f;
                startInvisible = false;
            }
        }

        protected IEnumerator SoundFollowsDebrisCenter(ISourceAudioEventReference instance,
            List<MoveBlockDebrisState> debris)
        {
            while (true && debris.Count > 0)
            {
                if (instance.PlaybackState == SourceAudioPlaybackState.Stopped)
                {
                    break;
                }

                Vector2 zero = Vector2.Zero;
                foreach (MoveBlockDebrisState debri in debris)
                {
                    zero += debri.Position;
                }

                zero /= debris.Count;
                SpatialAudio.Position(instance, zero);
                yield return null;
            }
        }
    }
}
