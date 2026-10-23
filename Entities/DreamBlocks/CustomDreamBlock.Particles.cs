using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Randomness;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Presentation.Hair;
using Celeste.Simulation.Gameplay.Player.Presentation.Sprites;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    public abstract partial class CustomDreamBlock
    {
        private static readonly string sourcePlayerRegistryKey =
            typeof(global::Celeste.Simulation.RoomPlayerSession).FullName;

        public virtual void SetupCustomParticles(float canvasWidth, float canvasHeight)
        {
            // Preserve the pinned source expression, including its comparison precedence.
            float countFactor = (FeatherMode ? .5f : .7f) * RefillCount != -1 ? 1.2f : 1f;
            AllocateParticles((int)(canvasWidth / 8f * (canvasHeight / 8f) * .7f * countFactor));
            if (!awake && RefillCount != -1)
            {
                delayedSetupParticles = true;
                return;
            }

            var dashColors = new PackedColor[3];
            if (RefillCount != -1)
            {
                RoomPlayerSession player = null;
                foreach (var entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                             sourcePlayerRegistryKey))
                    if (entity is RoomPlayerSession found)
                    {
                        player = found;
                        break;
                    }

                dashColors[0] = player == null ? PackedColor.White : GetHairColor(player, RefillCount);
                dashColors[1] = Lerp(dashColors[0], PackedColor.White, .5f);
                dashColors[2] = Lerp(dashColors[1], PackedColor.White, .5f);
            }

            for (int i = 0; i < particles.Length; i++)
            {
                int selected = RandomSource.Next(6);
                int layer = selected == 0 ? 0 : selected < 3 ? 1 : 2;
                particles[i] = new DreamParticle(this, i)
                {
                    Position = new Vector2(SourceRandomMath.NextFloat(RandomSource, canvasWidth),
                        SourceRandomMath.NextFloat(RandomSource, canvasHeight)),
                    Layer = layer, Color = GetParticleColor(layer, dashColors),
                    TimeOffset = SourceRandomMath.NextFloat(RandomSource)
                };
                if (FeatherMode)
                {
                    particles[i].Speed = SourceRandomMath.Range(RandomSource, 6f, 16f);
                    particles[i].Spin = SourceRandomMath.Range(RandomSource, 8f, 12f) * .2f;
                    particles[i].RotationCounter = SourceRandomMath.NextAngle(RandomSource);
                    particles[i].MaxRotate = SourceRandomMath.Range(RandomSource, .3f, .6f) * ((float)Math.PI / 2f);
                }
            }
        }

        private PackedColor GetParticleColor(int layer, PackedColor[] dashColors)
        {
            if (!PlayerHasDreamDash) return new PackedColor(211, 211, 211) * (.5f + layer / 2f * .5f);
            if (RefillCount != -1) return dashColors[layer];
            if (layer < 0 || layer > 2) throw new NotImplementedException();
            return DreamColors[layer * 3 + RandomSource.Next(3)];
        }

        internal static PackedColor GetHairColor(RoomPlayerSession player, int count)
        {
            bool badeline = player.Sprite.Mode == PlayerSpriteMode.MadelineAsBadeline;
            return count == 0
                ? badeline ? PlayerHairAppearance.UsedBadelineHairColor : PlayerHairAppearance.UsedHairColor
                : count == 1
                    ? badeline ? PlayerHairAppearance.NormalBadelineHairColor : PlayerHairAppearance.NormalHairColor
                    : badeline
                        ? PlayerHairAppearance.TwoDashesBadelineHairColor
                        : PlayerHairAppearance.TwoDashesHairColor;
        }

        protected virtual void UpdateParticles()
        {
            if (!PlayerHasDreamDash) return;
            for (int i = 0; i < particles.Length; i++)
            {
                Vector2 position = particles[i].Position;
                position.Y += .5f * particles[i].Speed * (1f / (.3f + .25f * particles[i].Layer)) * Engine.DeltaTime;
                particles[i].Position = position;
                particles[i].RotationCounter += particles[i].Spin * Engine.DeltaTime;
            }
        }

        private void ShakeParticles()
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 position, range;
                float angle, amount;
                switch (i)
                {
                    case 0:
                        position = CenterLeft + Vector2.UnitX;
                        range = Vector2.UnitY * (Height - 4f);
                        angle = (float)Math.PI;
                        amount = Height / 32f;
                        break;
                    case 1:
                        position = CenterRight;
                        range = Vector2.UnitY * (Height - 4f);
                        angle = 0;
                        amount = Height / 32f;
                        break;
                    case 2:
                        position = TopCenter + Vector2.UnitY;
                        range = Vector2.UnitX * (Width - 4f);
                        angle = -(float)Math.PI / 2f;
                        amount = Width / 32f;
                        break;
                    default:
                        position = BottomCenter;
                        range = Vector2.UnitX * (Width - 4f);
                        angle = (float)Math.PI / 2f;
                        amount = Width / 32f;
                        break;
                }

                particleRemainders[i] += amount * .25f;
                int emitted = (int)particleRemainders[i];
                particleRemainders[i] -= emitted;
                if (emitted > 0)
                    ((OriginalLevelSceneRoot)Scene).ParticlesBG.Emit(shakeParticle, emitted, position, range * .5f,
                        angle);
            }
        }
    }
}
