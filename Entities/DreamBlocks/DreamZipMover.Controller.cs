// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
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

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    public partial class DreamZipMover
    {
        private IEnumerator Sequence()
        {
            // Infinite.
            while (true)
            {
                if (!ShouldActivate(out bool isMainZipper))
                {
                    yield return null;
                    continue;
                }

                SignalLinkedZippers(true);

                Vector2 from = nodes[0];
                Vector2 to;
                float at;

                // Player is riding.
                bool shouldCancel = false;
                int i;
                for (i = 1; i < nodes.Length; i++)
                {
                    to = nodes[i];

                    // Start shaking.
                    if (isMainZipper)
                    {
                        sfx.Play("event:/CommunalHelperEvents/game/dreamZipMover/start");
                        ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Medium,
                            RumbleLength.Short);
                    }

                    StartShaking(0.1f);
                    yield return 0.1f;

                    SignalLinkedZippers(false);

                    // Start moving towards the target.
                    StopPlayerRunIntoAnimation = false;
                    at = 0f;
                    bool playedFinishSound = !isMainZipper;
                    while (at < 1f)
                    {
                        yield return null;
                        at = ScalarMath.Approach(at, 1f, 2f * Engine.DeltaTime);
                        if (at > impactSoundOffset && !playedFinishSound)
                        {
                            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                                "event:/CommunalHelperEvents/game/dreamZipMover/impact", Center);
                            playedFinishSound = true;
                        }

                        percent = ((ISourceEasingEnvironment)Engine).EasingArithmetic.SineIn(at);
                        Vector2 vector = Vector2Interpolation.Lerp(from, to, percent,
                            ((ISourceScalarEnvironment)Engine).ScalarArithmetic);
                        ScrapeParticlesCheck(to);

                        if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.1f, Engine))
                            pathRenderer?.CreateSparks();

                        MoveTo(vector);
                    }

                    bool last = i == nodes.Length - 1;

                    // Arrived, will wait for 0.5 secs.
                    StartShaking(0.2f);
                    ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Strong,
                        RumbleLength.Medium);
                    ((OriginalLevelSceneRoot)Scene).Shake();
                    StopPlayerRunIntoAnimation = true;
                    yield return 0.5f;

                    from = nodes[i];

                    if (ticking && !last)
                    {
                        float tickTime = 0.0f;
                        int tickNum = 0;
                        while (!ShouldActivate(out _) && tickNum < 5) // intentionally don't override isMainZipper here
                        {
                            yield return null;

                            tickTime = ScalarMath.Approach(tickTime, 1f, Engine.DeltaTime);
                            if (tickTime >= 1.0f)
                            {
                                tickTime = 0.0f;
                                tickNum++;
                                if (isMainZipper)
                                    ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                                        "event:/CommunalHelperEvents/game/dreamZipMover/tick", Center);
                                StartShaking(0.1f);
                            }
                        }

                        if (tickNum == 5 && !ShouldActivate(out isMainZipper))
                        {
                            shouldCancel = true;
                            break;
                        }

                        SignalLinkedZippers(true);
                    }
                    else if (waits && !last)
                    {
                        while (!ShouldActivate(out isMainZipper))
                            yield return null;

                        SignalLinkedZippers(true);
                    }
                }

                if (!permanent)
                {
                    if (noReturn)
                    {
                        Array.Reverse(nodes);
                    }
                    else
                    {
                        for (i -= 2 - (shouldCancel ? 1 : 0); i >= 0; i--)
                        {
                            to = nodes[i];

                            // Goes back to start with a speed that is four times slower.
                            StopPlayerRunIntoAnimation = false;
                            if (isMainZipper)
                                sfx.Play("event:/CommunalHelperEvents/game/dreamZipMover/return");
                            at = 0f;
                            bool playedFinishSound = !isMainZipper;
                            while (at < 1f)
                            {
                                yield return null;
                                at = ScalarMath.Approach(at, 1f, 0.5f * Engine.DeltaTime);
                                percent = 1f - ((ISourceEasingEnvironment)Engine).EasingArithmetic.SineIn(at);

                                Vector2 position = Vector2Interpolation.Lerp(from, to,
                                    ((ISourceEasingEnvironment)Engine).EasingArithmetic.SineIn(at),
                                    ((ISourceScalarEnvironment)Engine).ScalarArithmetic);
                                MoveTo(position);

                                if (at > impactSoundOffset && !playedFinishSound)
                                {
                                    playedFinishSound = true;
                                    ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                                        "event:/CommunalHelperEvents/game/dreamZipMover/finish", Center);
                                }
                            }

                            if (i != 0)
                                from = nodes[i];

                            StartShaking(0.2f);
                        }
                    }

                    StopPlayerRunIntoAnimation = true;

                    // Done, will not activate for 0.5 secs.
                    yield return 0.5f;
                }
                else
                {
                    // Done, will never be activated again.
                    StartShaking(0.3f);
                    if (isMainZipper)
                    {
                        ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                            "event:/CommunalHelperEvents/game/dreamZipMover/tick", Center);
                        ((OriginalLevelSceneRoot)Scene).Shake(0.15f);
                    }

                    while (true)
                        yield return null;
                }
            }
        }
    }
}
