using System;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Graphics.Animation;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Mods.CommunalHelper.Presentation.Refills;
using Celeste.Mods.CommunalHelper.Runtime.DreamTunnels;
using Celeste.Simulation;

namespace Celeste.Mods.CommunalHelper.Entities.Refills
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.DashStates.DreamTunnelRefill")]
    [CustomEntity("CommunalHelper/DreamRefill", "CommunalHelper/DreamTunnelRefill")]
    public sealed class DreamTunnelRefill : DashStateRefill
    {
        private readonly DreamTunnelDashRuntime runtime;
        private readonly DreamTunnelRefillParticles particles;
        private int glowParticleIndex;
        private int shatterParticleIndex;
        private int regenParticleIndex;

        public DreamTunnelRefill(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(engine, data, offset)
        {
            runtime = DreamTunnelDashRuntime.For(engine);
            particles = DreamTunnelRefillParticles.For(engine);
            Remove(sprite);
            Add(sprite = new Sprite(engine, this.engine.GameGraphics.GameAtlas,
                TwoDashes ? "objects/CommunalHelper/dreamRefillTwo/idle" : "objects/CommunalHelper/dreamRefill/idle"));
            sprite.AddLoop("idle", "", 0.1f);
            sprite.Play("idle");
            sprite.CenterOrigin();
        }

        protected override bool CanActivate(RoomPlayerSession player)
            => player.Data.Stamina < 20f || runtime.ChargeCount < (TwoDashes ? 2 : 1);

        protected override void Activated(RoomPlayerSession player)
        {
            if (TwoDashes)
            {
                runtime.ChargeCount = 2;
                player.Data.Dashes = Math.Max(player.Data.Dashes, 2);
            }
            else runtime.ChargeCount = 1;
        }

        protected override void EmitGlowParticles()
        {
            ((IOriginalRefillScene)Scene).ParticlesFG.Emit(particles.Glow[glowParticleIndex], 1, Position,
                Vector2.One * 5f);
            ++glowParticleIndex;
            glowParticleIndex %= 4;
        }

        protected override void EmitRegenParticles()
        {
            var level = (IOriginalRefillScene)Scene;
            for (int i = 0; i < 16; ++i)
            {
                level.ParticlesFG.Emit(particles.Regen[regenParticleIndex], 1, Position, Vector2.One * 2f);
                ++regenParticleIndex;
                regenParticleIndex %= 4;
            }
        }

        protected override void EmitShatterParticles(float angle)
        {
            var level = (IOriginalRefillScene)Scene;
            for (int i = 0; i < 5; ++i)
            {
                level.ParticlesFG.Emit(particles.Shatter[shatterParticleIndex], 1, Position, Vector2.One * 4f,
                    angle - (float)Math.PI / 2f);
                ++shatterParticleIndex;
                shatterParticleIndex %= 4;
            }

            for (int i = 0; i < 5; ++i)
            {
                level.ParticlesFG.Emit(particles.Shatter[shatterParticleIndex], 1, Position, Vector2.One * 4f,
                    angle + (float)Math.PI / 2f);
                ++shatterParticleIndex;
                shatterParticleIndex %= 4;
            }
        }
    }
}
