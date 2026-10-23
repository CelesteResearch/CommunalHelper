// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Gameplay.Collision;
using Celeste.Physics.Collision;
using Celeste.Mods.CommunalHelper.Content.Stations;
using Celeste.Mods.CommunalHelper.Runtime.Hazards;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using SoundSource = Celeste.Simulation.Audio.Sources.SoundSourceComponentState;
using Player = Celeste.Simulation.RoomPlayerSession;
using DashCollisionResults = Celeste.Simulation.Gameplay.Player.Collision.Surfaces.DashCollisionResult;
using Node = Celeste.Mods.CommunalHelper.Entities.Stations.StationBlockTrack.Node;

namespace Celeste.Mods.CommunalHelper.Entities.Stations
{
    public partial class StationBlock
    {
        private IEnumerator Sequence()
        {
            while (true)
            {
                while (!IsMoving)
                {
                    yield return null;
                }

                StartShaking(0.2f);
                ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Medium,
                    RumbleLength.Short);

                Vector2 dirSign = new Vector2(Math.Sign(MoveDir.X), Math.Sign(MoveDir.Y));
                float f = dirSign.X == -1 || dirSign.Y == -1 ? -1 : 1f;

                bool travel = nextNode is not null && CurrentNode != nextNode && currentTrack.CanBeUsed &&
                              !(currentTrack.OneWayDir.HasValue && currentTrack.OneWayDir.Value == -MoveDir);

                Sfx.Play(
                    "event:/CommunalHelperEvents/game/stationBlock/" + (Theme == Themes.Normal ? "station" : "moon") +
                    "_block_seq", "travel", travel ? 1f : 0f);
                Sfx.SetVolume(volume);

                if (travel)
                {
                    Safe = false;
                    if (CurrentNode.PushForce != Vector2.Zero)
                    {
                        ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                            "event:/CommunalHelperEvents/game/stationBlock/force_cue", Center);
                    }

                    arrowSprite.Play(GetTurnAnim(arrowDir, MoveDir), true);
                    yield return 0.2f;

                    float t = 0f;
                    StopPlayerRunIntoAnimation = false;
                    Vector2 start = CurrentNode.Center - offset;
                    Vector2 target = nextNode.Center - offset;

                    while (t < 1f)
                    {
                        float tNextUnclamped = t + (speedFactor * 2f * Engine.DeltaTime);
                        t = ScalarMath.Approach(t, 1f, speedFactor * 2f * Engine.DeltaTime);

                        Percent = ((ISourceEasingEnvironment)Engine).EasingArithmetic.SineIn(t);
                        currentTrack.TrackOffset = f * Percent * 16;
                        CurrentNode.Percent = nextNode.Percent = currentTrack.Percent = Percent;

                        if (nextNode.HasIndicator && nextNode.PushForce != Vector2.Zero)
                        {
                            nextNode.ColorLerp = Math.Min(1f, t * 2f);
                        }

                        Vector2 to = Vector2Interpolation.Lerp(start, target, Percent,
                            ((ISourceScalarEnvironment)Engine).ScalarArithmetic);
                        ScrapeParticlesCheck(to);
                        if (SceneClock.OnInterval(0.05f, Engine))
                        {
                            currentTrack.CreateSparks(Center, p_sparks);
                        }

                        Vector2 toUnclamped = Vector2Interpolation.Lerp(start, target,
                            ((ISourceEasingEnvironment)Engine).EasingArithmetic.SineIn(tNextUnclamped),
                            ((ISourceScalarEnvironment)Engine).ScalarArithmetic);
                        Vector2 theoreticalLiftSpeed = Engine.DeltaTime == 0f
                            ? Vector2.Zero
                            : ((ISourceVectorEnvironment)Engine).VectorArithmetic.Divide(toUnclamped - ExactPosition,
                                Engine.DeltaTime);
                        MoveToX(to.X, theoreticalLiftSpeed.X);
                        MoveToY(to.Y, theoreticalLiftSpeed.Y);
                        yield return null;
                    }

                    StartShaking(0.2f);
                    Sfx.Param(Theme == Themes.Moon ? "end_moon" : "end", 1);
                    ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Strong,
                        RumbleLength.Medium);
                    Level.Shake(0.2f);
                    StopPlayerRunIntoAnimation = true;

                    currentTrack.TrackOffset = 0f;
                    CurrentNode.Percent = nextNode.Percent = currentTrack.Percent = Percent = 0f;
                    CurrentNode = nextNode;
                }
                else
                {
                    arrowSprite.Play(GetAnimName(arrowDir, arrowDir), true);
                    yield return 0.25f;
                }

                Safe = true;
                IsMoving = false;
            }
        }
    }
}
