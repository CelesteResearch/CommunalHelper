using System.Collections;
using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Components.Animation;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Modules.Updating;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Session;
using Celeste.Game.Gameplay.Mechanisms.SwitchGates;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.CommunalHelper.Runtime.DreamBlocks.Gates;
using Celeste.Game.Components.Switches;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using SoundSource = Celeste.Simulation.Audio.Sources.SoundSourceComponentState;
using Wiggler = Celeste.Scene.Components.Oscillation.WigglerComponent;
using Tween = Celeste.Core.Components.Animation.SourceTweenState;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    public partial class DreamSwitchGate
    {
        private IEnumerator MaxHelpingHandSequence(Vector2 node, bool goingBack)
        {
            Vector2 start = Position;

            Color fromColor, toColor;

            if (!goingBack)
            {
                fromColor = inactiveColor;
                toColor = finishColor;
                while ((!Triggered || allowReturn) && !Level.Session.GetFlag(Flag))
                {
                    yield return null;
                }
            }
            else
            {
                fromColor = finishColor;
                toColor = inactiveColor;
                while (Level.Session.GetFlag(Flag))
                {
                    yield return null;
                }
            }

            yield return 0.1f;
            if (ShouldCancelMove(goingBack))
                yield break;

            // animate the icon
            openSfx.Play(moveSound);
            if (shakeTime > 0f)
            {
                StartShaking(shakeTime);
                while (icon.Rate < 1f)
                {
                    icon.Color = Lerp(fromColor, activeColor, icon.Rate);
                    icon.Rate += Engine.DeltaTime / shakeTime;
                    yield return null;
                    if (ShouldCancelMove(goingBack))
                        yield break;
                }
            }
            else
            {
                icon.Rate = 1f;
            }

            yield return 0.1f;
            if (ShouldCancelMove(goingBack))
                yield break;

            // move the switch gate, emitting particles along the way
            int particleAt = 0;
            Tween tween = SourceTweenState.Create(process.TweenPool, SourceTweenState.TweenMode.Oneshot,
                moveEased ? process.EasingArithmetic.CubeOut : null, moveTime + (moveEased ? 0.2f : 0f), start: true);
            tween.OnUpdate = tweenArg =>
            {
                MoveTo(Vector2Math.Lerp(start, node, tweenArg.Eased, process.ScalarArithmetic));
                if (Level.SourceScene.Clock.OnInterval(0.1f, Engine))
                {
                    particleAt++;
                    particleAt %= 2;
                    for (int tileX = 0; tileX < Width / 8f; tileX++)
                    {
                        for (int tileY = 0; tileY < Height / 8f; tileY++)
                        {
                            if ((tileX + tileY) % 2 == particleAt)
                            {
                                Level.ParticlesBG.Emit(process.ParticleTypes.SwitchGate.P_Behind,
                                    Position + new Vector2(tileX * 8, tileY * 8) +
                                    SourceRandomMath.Range(RandomSource, Vector2.One * 2f, Vector2.One * 6f));
                            }
                        }
                    }
                }
            };
            OwnedComponents.Add(tween);

            float moveTimeLeft = moveTime;
            while (moveTimeLeft > 0f)
            {
                yield return null;
                moveTimeLeft -= Engine.DeltaTime;
                if (ShouldCancelMove(goingBack, tween))
                    yield break;
            }

            bool collidableBackup = Collidable;
            Collidable = false;

            // collide dust particles on the left
            Celeste.Game.Gameplay.Mechanisms.SwitchGates.Effects.SwitchGateParticleEffects.EmitEdgeDust(this, start,
                node, process);
            Collidable = collidableBackup;

            // moving is over
            process.AudioSession.Play(finishedSound, Position, process.SaveContext);
            StartShaking(0.2f);
            while (icon.Rate > 0f)
            {
                icon.Color = Lerp(activeColor, toColor, 1f - icon.Rate);
                icon.Rate -= Engine.DeltaTime * 4f;
                yield return null;
                if (ShouldCancelMove(goingBack))
                    yield break;
            }

            icon.Rate = 0f;
            icon.SetAnimationFrame(0);
            wiggler.Start();

            // emit fire particles if the block is not behind a solid.
            collidableBackup = Collidable;
            Collidable = false;
            if (SourceSceneCollision.FirstPoint<ISourceEntityCollider>(Level.Scheduler.Entities, "Celeste.Solid",
                    Center) == null)
            {
                for (int i = 0; i < 32; i++)
                {
                    float angle = SourceRandomMath.NextFloat(RandomSource, (float)Math.PI * 2f);
                    Level.ParticlesFG.Emit(goingBack ? P_RecoloredFireBack : P_RecoloredFire,
                        Position + iconOffset + Vector2Geometry.AngleToVector(angle, 4f), angle);
                }
            }

            Collidable = collidableBackup;
        }

        private bool ShouldCancelMove(bool goingBack, Tween tween = null)
        {
            if (allowReturn && Level.Session.GetFlag(Flag) == goingBack)
            {
                // whoops, the flag changed too fast! we need to backtrack.
                if (tween is not null)
                {
                    OwnedComponents.Remove(tween);
                }

                icon.Rate = 0f;
                icon.SetAnimationFrame(0);
                return true;
            }

            return false;
        }
    }
}
