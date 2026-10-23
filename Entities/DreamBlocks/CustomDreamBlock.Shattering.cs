using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    public abstract partial class CustomDreamBlock
    {
        internal bool SkipExitDashRefill => RefillCount == -2;
        protected bool ShatterCheck() => !shattering;

        public virtual void BeginShatter()
        {
            if (!ShatterCheck()) return;
            shattering = true;
            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                "event:/CommunalHelperEvents/game/connectedDreamBlock/dreamblock_shatter", Position);
            Add(new SimulationCoroutine(Engine, ShatterSequence()));
        }

        private IEnumerator ShatterSequence()
        {
            if (QuickDestroy)
            {
                Collidable = false;
                foreach (var mover in staticMovers) ((SceneEntityState)mover.Entity).Collidable = false;
            }
            else yield return .28f;

            while (ColorLerp < 2f)
            {
                ColorLerp += Engine.DeltaTime * 10f;
                yield return null;
            }

            ColorLerp = 1f;
            if (!QuickDestroy) yield return .05f;
            var level = (OriginalLevelSceneRoot)Scene;
            level.Shake(.65f);
            Vector2 camera = level.Camera.Position;
            for (int i = 0; i < particles.Length; i++)
            {
                Vector2 position = PutInside(particles[i].Position + camera * (.3f + .25f * particles[i].Layer));
                var type = new ParticleTypePresentation(((IOriginalGameplayProcess)Engine).ParticleTypes.Lightning
                    .P_Shatter)
                {
                    ColorMode = ParticleTypePresentation.ColorModes.Fade,
                    Color = particles[i].Color, Color2 = Lerp(particles[i].Color, PackedColor.White, .6f),
                    Source = FeatherMode ? featherTextures[particles[i].Layer] : particleTextures[2],
                    SpinMax = FeatherMode ? (float)Math.PI : 0f,
                    RotationMode = FeatherMode
                        ? ParticleTypePresentation.RotationModes.Random
                        : ParticleTypePresentation.RotationModes.None,
                    Direction = Vector2Geometry.Angle(position - Center)
                };
                level.ParticlesFG.Emit(type, 1, position, Vector2.One * 3f);
            }

            OneUseDestroy();
            Engine.Effects.Glitch = .22f;
            while (Engine.Effects.Glitch > 0f)
            {
                Engine.Effects.Glitch -= .5f * Engine.DeltaTime;
                yield return null;
            }

            Engine.Effects.Glitch = 0f;
            RemoveSelf();
        }

        // This is the mod's post-animation finale, separate from vanilla's private entry point.
        protected virtual void OneUseDestroy()
        {
            Collidable = Visible = false;
            DisableStaticMovers();
        }

        public override void OnPlayerExit(RoomPlayerSession player)
        {
            base.OnPlayerExit(player);
            if (RefillCount < 0) return;
            player.Data.Dashes = RefillCount;
            PackedColor color = GetHairColor(player, RefillCount);
            var type = new ParticleTypePresentation(
                ((IOriginalGameplayProcess)Engine).ParticleTypes.Refill.P_ShatterTwo)
            {
                Friction = 2f, LifeMin = .4f, LifeMax = .6f,
                Color = Lerp(color, PackedColor.White, .5f), Color2 = color,
                ColorMode = ParticleTypePresentation.ColorModes.Choose
            };
            ((OriginalLevelSceneRoot)player.Scene).ParticlesFG.Emit(type, 5, player.Position, Vector2.Zero,
                Vector2Geometry.Angle(player.Data.Direction));
            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                "event:/new_content/game/10_farewell/pinkdiamond_touch", player.Position);
        }
    }
}
