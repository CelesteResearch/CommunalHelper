using System;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Gameplay.Collectibles.Refills;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;

namespace Celeste.Mods.CommunalHelper.Entities.Refills
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.DashStates.DashStateRefill")]
    public abstract class DashStateRefill : OriginalRefillState
    {
        private readonly float respawnTime;
        protected string TouchSFX = "event:/CommunalHelperEvents/game/dreamRefill/dream_refill_touch";
        internal string ReturnSFX = "event:/CommunalHelperEvents/game/dreamRefill/dream_refill_return";
        internal ISourceEngineContext OwnedEngine { get; }

        protected DashStateRefill(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(data, offset, (IOriginalRefillProcess)engine)
        {
            OwnedEngine = engine;
            respawnTime = EntityAttributeReader.Float(data.Values, "respawnTime", 2.5f);
            Get<PlayerColliderComponentState>().OnCollide = OnPlayer;
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            if (CanActivate(player))
            {
                player.RefillDash();
                player.RefillStamina();
                Activated(player);
                engine.SpatialAudio.Play(TouchSFX, Position);
                engine.RumbleSession.Rumble(1, 1);
                Collidable = false;
                OwnedComponents.Add(new SimulationCoroutine(engine, RefillRoutine(player)));
                respawnTimer = respawnTime;
            }
        }

        protected abstract bool CanActivate(RoomPlayerSession player);
        protected abstract void Activated(RoomPlayerSession player);

        protected virtual void EmitGlowParticles()
            => ((IOriginalRefillScene)Scene).ParticlesFG.Emit(p_glow, 1, Position, Vector2.One * 5f);

        protected virtual void EmitRegenParticles()
            => ((IOriginalRefillScene)Scene).ParticlesFG.Emit(p_regen, 16, Position, Vector2.One * 2f);

        protected virtual void EmitShatterParticles(float angle)
        {
            var level = (IOriginalRefillScene)Scene;
            level.ParticlesFG.Emit(p_shatter, 5, Position, Vector2.One * 4f, angle - (float)Math.PI / 2f);
            level.ParticlesFG.Emit(p_shatter, 5, Position, Vector2.One * 4f, angle + (float)Math.PI / 2f);
        }

        internal void EmitOwnedGlowParticles() => EmitGlowParticles();
        internal void EmitOwnedRegenParticles() => EmitRegenParticles();
        internal void EmitOwnedShatterParticles(float angle) => EmitShatterParticles(angle);
    }
}
