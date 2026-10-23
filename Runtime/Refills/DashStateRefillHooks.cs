using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Scene.Components.Oscillation;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Display;
using Celeste.Mods.CommunalHelper.Entities.Refills;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Particles;
using Celeste.Simulation.Presentation.Particles.Definitions;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.Refills
{
    public sealed class DashStateRefillHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();

        private delegate void UpdateGlow(SceneEntityState owner, IOriginalRefillProcess process,
            IOriginalRefillScene level,
            ref float timer, Action respawn, ParticleTypePresentation glow);

        private delegate void RespawnRefill(SceneEntityState owner, IOriginalRefillProcess process,
            IOriginalRefillScene level,
            Sprite sprite, SpriteImage outline, WigglerComponent wiggler, ParticleTypePresentation regen, string sound);

        private delegate void Finish(SceneEntityState owner, IOriginalRefillProcess process, IOriginalRefillScene level,
            ParticleTypePresentation shatter, RoomPlayerSession player);

        public DashStateRefillHooks(ISourceEngineContext engine)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            try
            {
                hooks.Add(new Hook(typeof(RefillPresentation).GetMethod(nameof(RefillPresentation.UpdateTimerAndGlow),
                        new[]
                        {
                            typeof(SceneEntityState), typeof(IOriginalRefillProcess), typeof(IOriginalRefillScene),
                            typeof(float).MakeByRefType(), typeof(Action), typeof(ParticleTypePresentation)
                        }),
                    (UpdateGlowHook)UpdateTimerAndGlow));
                hooks.Add(new Hook(typeof(RefillPresentation).GetMethod(nameof(RefillPresentation.Respawn),
                        new[]
                        {
                            typeof(SceneEntityState), typeof(IOriginalRefillProcess), typeof(IOriginalRefillScene),
                            typeof(Sprite), typeof(SpriteImage), typeof(WigglerComponent),
                            typeof(ParticleTypePresentation), typeof(string)
                        }),
                    (RespawnHook)Respawn));
                hooks.Add(new Hook(typeof(RefillPresentation).GetMethod(nameof(RefillPresentation.FinishRoutine),
                        new[]
                        {
                            typeof(SceneEntityState), typeof(IOriginalRefillProcess), typeof(IOriginalRefillScene),
                            typeof(ParticleTypePresentation), typeof(RoomPlayerSession)
                        }),
                    (Action<Finish, SceneEntityState, IOriginalRefillProcess, IOriginalRefillScene,
                        ParticleTypePresentation, RoomPlayerSession>)FinishRoutine));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private delegate void UpdateGlowHook(UpdateGlow orig, SceneEntityState owner, IOriginalRefillProcess process,
            IOriginalRefillScene level, ref float timer, Action respawn, ParticleTypePresentation glow);

        private delegate void RespawnHook(RespawnRefill orig, SceneEntityState owner, IOriginalRefillProcess process,
            IOriginalRefillScene level, Sprite sprite, SpriteImage outline, WigglerComponent wiggler,
            ParticleTypePresentation regen, string sound);

        private bool Owned(SceneEntityState owner, out DashStateRefill refill)
        {
            refill = owner as DashStateRefill;
            return refill != null && ReferenceEquals(refill.OwnedEngine, engine);
        }

        private void UpdateTimerAndGlow(UpdateGlow orig, SceneEntityState owner, IOriginalRefillProcess process,
            IOriginalRefillScene level, ref float timer, Action respawn, ParticleTypePresentation glow)
        {
            if (Owned(owner, out var refill))
                RefillPresentation.UpdateTimerAndGlow(owner, process, level, ref timer, respawn, glow,
                    new OwnedEmission(refill, false));
            else orig(owner, process, level, ref timer, respawn, glow);
        }

        private void Respawn(RespawnRefill orig, SceneEntityState owner, IOriginalRefillProcess process,
            IOriginalRefillScene level, Sprite sprite, SpriteImage outline, WigglerComponent wiggler,
            ParticleTypePresentation regen, string sound)
        {
            if (Owned(owner, out var refill))
                RefillPresentation.Respawn(owner, process, level, sprite, outline, wiggler, regen,
                    sound, -100,
                    new OwnedEmission(refill, true), new OwnedReturnSound(refill));
            else orig(owner, process, level, sprite, outline, wiggler, regen, sound);
        }

        private readonly struct OwnedReturnSound : IRefillReturnSound
        {
            private readonly DashStateRefill refill;
            public OwnedReturnSound(DashStateRefill refill) => this.refill = refill;

            public string Resolve(string defaultSound)
                => defaultSound == "event:/game/general/diamond_return" ? refill.ReturnSFX : defaultSound;
        }

        private readonly struct OwnedEmission : IRefillParticleEmission
        {
            private readonly DashStateRefill refill;
            private readonly bool regen;

            public OwnedEmission(DashStateRefill refill, bool regen)
            {
                this.refill = refill;
                this.regen = regen;
            }

            public void Emit(ParticleSystemPresentation particles, ParticleTypePresentation type, int amount,
                Vector2 position, Vector2 range)
            {
                if (regen) refill.EmitOwnedRegenParticles();
                else refill.EmitOwnedGlowParticles();
            }

            public void Emit(ParticleSystemPresentation particles, ParticleTypePresentation type, int amount,
                Vector2 position, Vector2 range, float direction)
                => particles.Emit(type, amount, position, range, direction);
        }

        private void FinishRoutine(Finish orig, SceneEntityState owner, IOriginalRefillProcess process,
            IOriginalRefillScene level, ParticleTypePresentation shatter, RoomPlayerSession player)
        {
            if (Owned(owner, out var refill))
            {
                refill.EmitOwnedShatterParticles(Vector2Geometry.Angle(player.Speed));
                RefillPresentation.FinishRoutine(owner, process, level, shatter, 0f, false);
            }
            else orig(owner, process, level, shatter, player);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; --i) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
