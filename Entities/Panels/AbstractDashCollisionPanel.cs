// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Physics.Collision;
using Celeste.Mods.CommunalHelper.Runtime.Panels;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;
using Directions = Celeste.Simulation.Gameplay.Hazards.Spikes.SpikeDirection;

namespace Celeste.Mods.CommunalHelper.Entities.Panels
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.AbstractDashCollisionPanel")]
    public abstract class AbstractDashCollisionPanel : AbstractPanel
    {
        protected AbstractDashCollisionPanel(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            engine, data, offset)
        {
        }

        protected AbstractDashCollisionPanel(ISourceEngineContext engine, Vector2 position, float size,
            Directions orientation,
            bool overrideAllowStaticMovers) : base(engine, position, size, orientation, overrideAllowStaticMovers)
        {
        }

        protected override void OnAttach(SourcePlatformEntityState platform)
        {
            base.OnAttach(platform);
            platform.OnDashCollide = DashCollisionHooks.Apply(platform.OnDashCollide, OnDashCollide);
        }

        protected virtual DashCollisionResult OnDashCollide(Func<RoomPlayerSession, Vector2, DashCollisionResult> orig,
            RoomPlayerSession player, Vector2 dir) => orig(player, dir);

        protected bool CheckDashCollision(RoomPlayerSession player, Vector2 dir)
        {
            switch (Orientation)
            {
                case Directions.Up when dir.Y > 0:
                case Directions.Down when dir.Y < 0:
                case Directions.Left when dir.X > 0:
                case Directions.Right when dir.X < 0:
                    return SourceEntityCollision.CheckAt(player, this, player.Position + dir);
            }

            return false;
        }

        public override void Removed(object scene)
        {
            if (staticMover.Platform is SourcePlatformEntityState platform &&
                (platform.Tags.Bits & PersistentMask) != 0)
                DashCollisionHooks.Remove(platform.OnDashCollide, OnDashCollide);
            base.Removed(scene);
        }
    }
}
