// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;

namespace Celeste.Mods.CommunalHelper.Entities.Panels
{
    [CustomEntity("CommunalHelper/DashCollisionPanel")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.DashCollisionPanel")]
    public class DashCollisionPanel : AbstractDashCollisionPanel
    {
        public DashCollisionResult dashCollisionOverride;
        public bool overrideCollision;

        public static DashCollisionPanel Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public DashCollisionPanel(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(engine, data,
            offset)
        {
            overrideCollision = EntityAttributeReader.Bool(data.Values, "overrideCollision");
            dashCollisionOverride =
                EntityAttributeReader.Enum(data.Values, "dashCollideResult", DashCollisionResult.NormalCollision);
        }

        protected override DashCollisionResult OnDashCollide(Func<RoomPlayerSession, Vector2, DashCollisionResult> orig,
            RoomPlayerSession player, Vector2 dir)
        {
            if (CheckDashCollision(player, dir))
            {
                if (!overrideCollision) base.OnDashCollide(orig, player, dir);
                return dashCollisionOverride;
            }

            return base.OnDashCollide(orig, player, dir);
        }
    }
}
