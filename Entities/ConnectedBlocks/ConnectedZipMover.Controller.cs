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

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedZipMover
    {
        private IEnumerator Sequence()
        {
            // Infinite.
            while (true)
            {
                if (!HasPlayerRider())
                {
                    yield return null;
                    continue;
                }

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
                    sfx.Play($"event:/CommunalHelperEvents/game/zipMover/{sfxPath}/start");
                    ((IOriginalGameInputProcessEnvironment)Engine).InputSource.Rumble(RumbleStrength.Medium,
                        RumbleLength.Short);
                    StartShaking(0.1f);
                    yield return 0.1f;

                    // Start moving towards the target.
                    streetlight.SetAnimationFrame(3);
                    StopPlayerRunIntoAnimation = false;
                    at = 0f;
                    while (at < 1f)
                    {
                        yield return null;
                        Vector2 vector = ZipMoverMotion.Forward(Engine, from, to, ref at, ref percent);

                        if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.1f, Engine))
                            pathRenderer.CreateSparks();

                        MoveTo(vector);

                        if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.03f, Engine))
                            SpawnScrapeParticles();
                    }

                    bool last = i == nodes.Length - 1;

                    // Arrived, will wait for 0.5 secs.
                    StartShaking(0.2f);
                    ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                        $"event:/CommunalHelperEvents/game/zipMover/{sfxPath}/impact", Center);
                    streetlight.SetAnimationFrame(((waits && !last) || (ticking && !last) || (permanent && last))
                        ? 1
                        : 2);
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
                        while (!HasPlayerRider() && tickNum < 5)
                        {
                            yield return null;
                            streetlight.SetAnimationFrame(1 - (int)Math.Round(tickTime));


                            tickTime = ScalarMath.Approach(tickTime, 1f, Engine.DeltaTime,
                                ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic);
                            if (tickTime >= 1.0f)
                            {
                                tickTime = 0.0f;
                                ++tickNum;
                                ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                                    $"event:/CommunalHelperEvents/game/zipMover/{sfxPath}/tick", Center);
                                StartShaking(0.1f);
                            }
                        }

                        if (tickNum == 5 && !HasPlayerRider())
                        {
                            shouldCancel = true;
                            break;
                        }
                    }
                    else if (waits && !last)
                    {
                        streetlight.SetAnimationFrame(1);
                        while (!HasPlayerRider())
                            yield return null;
                    }
                }

                if (!permanent)
                {
                    for (i -= 2 - (shouldCancel ? 1 : 0); i >= 0; i--)
                    {
                        to = nodes[i];

                        // Goes back to start with a speed that is four times slower.
                        StopPlayerRunIntoAnimation = false;
                        streetlight.SetAnimationFrame(2);
                        sfx.Play($"event:/CommunalHelperEvents/game/zipMover/{sfxPath}/return");
                        at = 0f;
                        while (at < 1f)
                        {
                            yield return null;
                            Vector2 position = ZipMoverMotion.Backward(Engine, from, to, ref at, ref percent);
                            MoveTo(position);
                        }

                        if (i != 0)
                            from = nodes[i];

                        StartShaking(0.2f);
                        ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                            $"event:/CommunalHelperEvents/game/zipMover/{sfxPath}/finish", Center);
                    }

                    StopPlayerRunIntoAnimation = true;

                    // Done, will not activate for 0.5 secs.
                    streetlight.SetAnimationFrame(1);
                    yield return 0.5f;
                }
                else
                {
                    // Done, will never be activated again.
                    StartShaking(0.3f);
                    ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                        $"event:/CommunalHelperEvents/game/zipMover/{sfxPath}/finish", Center);
                    ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                        $"event:/CommunalHelperEvents/game/zipMover/{sfxPath}/tick", Center);
                    ((OriginalLevelSceneRoot)Scene).Shake(0.15f);
                    streetlight.SetAnimationFrame(0);
                    while (true)
                        yield return null;
                }
            }
        }
    }
}
