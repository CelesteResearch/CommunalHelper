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
        private IEnumerator MoveBackAndForthSequence(Vector2 position, Vector2 node, bool startAtNode)
        {
            while (true)
            {
                if (!startAtNode)
                {
                    // go forth
                    yield return new SourceSwapImmediately(MaxHelpingHandSequence(node, goingBack: false));
                }

                // go back
                yield return new SourceSwapImmediately(MaxHelpingHandSequence(position, goingBack: true));
                startAtNode = false;
            }
        }

        private IEnumerator CommunalHelperSequence(Vector2 node)
        {
            this.node = node;

            Vector2 start = Position;
            while (!OriginalSwitchComponent.Check(Scene))
            {
                yield return null;
            }

            if (permanent)
            {
                OriginalSwitchComponent.SetLevelFlag(Level);
            }

            yield return 0.1f;

            openSfx.Play("event:/game/general/touchswitch_gate_open");
            StartShaking(0.5f);
            while (icon.Rate < 1f)
            {
                icon.Color = Lerp(inactiveColor, activeColor, icon.Rate);
                icon.Rate += Engine.DeltaTime * 2f;
                yield return null;
            }

            yield return 0.1f;


            int particleAt = 0;
            Tween tween = SourceTweenState.Create(process.TweenPool, SourceTweenState.TweenMode.Oneshot,
                process.EasingArithmetic.CubeOut, 2f, start: true);
            tween.OnUpdate = t =>
            {
                MoveTo(Vector2Math.Lerp(start, node, t.Eased, process.ScalarArithmetic));
                if (Level.SourceScene.Clock.OnInterval(0.1f, Engine))
                {
                    particleAt++;
                    particleAt %= 2;
                    for (int n = 0; n < Width / 8f; n++)
                    {
                        for (int num2 = 0;
                             num2 < Height / 8f;
                             num2++)
                        {
                            if ((n + num2) % 2 == particleAt)
                            {
                                ParticleType pType = RandomSource.Choose(P_BehindDreamParticles);
                                Level.ParticlesBG.Emit(pType,
                                    Position + new Vector2(n * 8, num2 * 8) +
                                    SourceRandomMath.Range(RandomSource, Vector2.One * 2f, Vector2.One * 6f));
                            }
                        }
                    }
                }
            };
            OwnedComponents.Add(tween);
            yield return 1.8f;

            bool collidable = Collidable;
            Collidable = false;
            Celeste.Game.Gameplay.Mechanisms.SwitchGates.Effects.SwitchGateParticleEffects.EmitEdgeDust(this, start,
                node, process);
            Collidable = collidable;
            process.AudioSession.Play("event:/game/general/touchswitch_gate_finish", Position, process.SaveContext);
            StartShaking(0.2f);
            while (icon.Rate > 0f)
            {
                icon.Color = Lerp(activeColor, finishColor, 1f - icon.Rate);
                icon.Rate -= Engine.DeltaTime * 4f;
                yield return null;
            }

            icon.Rate = 0f;
            icon.SetAnimationFrame(0);
            wiggler.Start();
            bool collidable2 = Collidable;
            Collidable = false;
            if (SourceSceneCollision.FirstPoint<ISourceEntityCollider>(Level.Scheduler.Entities, "Celeste.Solid",
                    Center) == null)
            {
                for (int m = 0; m < 32; m++)
                {
                    float num = SourceRandomMath.NextFloat(RandomSource, (float)Math.PI * 2f);
                    Level.ParticlesFG.Emit(process.ParticleTypes.TouchSwitch.P_Fire,
                        Center + Vector2Geometry.AngleToVector(num, 4f), num);
                }
            }

            Collidable = collidable2;
        }
    }
}
