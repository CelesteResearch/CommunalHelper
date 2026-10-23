// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Maps;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Core.Rendering;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Game.Gameplay.Interaction.Springs;
using Celeste.Game.Presentation.Platforms.Cassette;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Hazards.Spikes;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Mods.CommunalHelper.Presentation.Colors;
using Celeste.Mods.CommunalHelper.Runtime.CassetteBlocks;
using Color = Celeste.Core.Display.PackedColor;
using Image = Celeste.Graphics.Animation.SpriteImage;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Rectangle = Celeste.Core.Geometry.IntRect;
using System.Collections;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Game.Controls.Source;
using Celeste.Game.Entities.Platforms.ZipMovers.Motion;
using Celeste.Game.Presentation.Platforms.ZipMovers;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Presentation.Particles.Definitions;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    public partial class CassetteZipMover
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
                    sfx.Play("event:/CommunalHelperEvents/game/zipMover/normal/start");
                    ((IOriginalGameInputProcessEnvironment)EngineContext).InputSource.Rumble(RumbleStrength.Medium,
                        RumbleLength.Short);
                    StartShaking(0.1f);
                    yield return 0.1f;

                    // Start moving towards the target.
                    StopPlayerRunIntoAnimation = false;
                    at = 0f;
                    while (at < 1f)
                    {
                        yield return null;
                        Vector2 vector = ZipMoverMotion.Forward(EngineContext, from, to, ref at, ref percent);
                        vector = FixCassetteY(vector);

                        ScrapeParticlesCheck(to);
                        if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.1f, EngineContext))
                            pathRenderer.CreateSparks();

                        MoveTo(vector);
                    }

                    bool last = i == nodes.Length - 1;

                    // Arrived, will wait for 0.5 secs.
                    StartShaking(0.2f);
                    ((ISourceSpatialAudioEnvironment)EngineContext).SpatialAudio.Play(
                        "event:/CommunalHelperEvents/game/zipMover/normal/impact", Center);
                    ((IOriginalGameInputProcessEnvironment)EngineContext).InputSource.Rumble(RumbleStrength.Strong,
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

                            tickTime = ScalarMath.Approach(tickTime, 1f, EngineContext.DeltaTime,
                                ((ISourceExtremaEnvironment)EngineContext).ExtremaArithmetic);
                            if (tickTime >= 1.0f)
                            {
                                tickTime = 0.0f;
                                tickNum++;
                                sfx.Play("event:/CommunalHelperEvents/game/zipMover/normal/tick");
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
                        while (!HasPlayerRider())
                            yield return null;
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
                        for (i -= 2 - (shouldCancel ? 1 : 0); i >= 0; --i)
                        {
                            to = nodes[i];

                            // Goes back to start with a speed that is four times slower.
                            StopPlayerRunIntoAnimation = false;
                            //streetlight.SetAnimationFrame(2);
                            sfx.Play("event:/CommunalHelperEvents/game/zipMover/normal/return");
                            at = 0f;
                            while (at < 1f)
                            {
                                yield return null;
                                Vector2 position =
                                    ZipMoverMotion.Backward(EngineContext, from, to, ref at, ref percent);
                                position = FixCassetteY(position);

                                MoveTo(position);
                            }

                            if (i != 0)
                                from = nodes[i];

                            StartShaking(0.2f);
                            ((ISourceSpatialAudioEnvironment)EngineContext).SpatialAudio.Play(
                                "event:/CommunalHelperEvents/game/zipMover/normal/finish", Center);
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
                    ((ISourceSpatialAudioEnvironment)EngineContext).SpatialAudio.Play(
                        "event:/CommunalHelperEvents/game/zipMover/normal/finish", Center);
                    ((ISourceSpatialAudioEnvironment)EngineContext).SpatialAudio.Play(
                        "event:/CommunalHelperEvents/game/zipMover/normal/tick", Center);
                    ((OriginalLevelSceneRoot)Scene).Shake(0.15f);
                    while (true)
                        yield return null;
                }
            }
        }

        private Vector2 FixCassetteY(Vector2 vec)
        {
            return vec + BlockOffset;
        }
    }
}
