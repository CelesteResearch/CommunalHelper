// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Graphics.Display.Colors;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Physics.Collision;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Rectangle = Celeste.Core.Geometry.IntRect;

namespace Celeste.Mods.CommunalHelper.Entities.MoveBlocks
{
    [CustomEntity("CommunalHelper/MoveBlockGroup")]
    public class MoveBlockGroup : SceneEntityState
    {
        public enum RespawnBehavior
        {
            Immediate,
            Simultaneous,
            Sequential,
        }

        private static readonly PackedColor defaultColor = SourceHexColor.HexToColor("ffae11");

        private readonly Vector2[] nodes;

        public PackedColor Color { get; }
        public bool DisablePulse { get; }
        public bool SyncActivation { get; }
        private readonly RespawnBehavior respawnBehavior;

        private readonly List<GroupableMoveBlock> components = new();

        public static MoveBlockGroup Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
            => new MoveBlockGroup(
                ((ISourceEntitySourceDataEnvironment)level.EngineContext).EntitySourceDataRegistration,
                data.NodesOffset(offset), SourceHexColor.HexToColor(data.Attr("color", "ffae11")),
                data.Bool("disablePulse"),
                data.Bool("syncActivation", true), data.Enum("respawnBehavior", RespawnBehavior.Simultaneous));

        public MoveBlockGroup(ISourceEntitySourceDataRegistration sourceData, Vector2[] nodes, PackedColor color,
            bool disablePulse = false, bool syncActivation = true,
            RespawnBehavior respawnBehavior = RespawnBehavior.Simultaneous)
            : base(sourceData)
        {
            this.nodes = nodes;
            Color = color;
            DisablePulse = disablePulse;
            SyncActivation = syncActivation;
            this.respawnBehavior = respawnBehavior;
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);

            foreach (Vector2 node in nodes)
            {
                Rectangle hitbox = new((int)node.X - 4, (int)node.Y - 4, 8, 8);
                foreach (GroupableMoveBlock c in GetGroupableComponents(scene).Cast<GroupableMoveBlock>())
                {
                    // force CassetteMoveBlocks to be detected as they will be uncollidable until the cassette music starts
                    if ((c.AllowGroupingWhenNoncollidable || ((SceneEntityState)c.Entity).Collidable) &&
                        SourceEntityGeometryCollision.Rectangle((SceneEntityState)c.Entity, hitbox) &&
                        !components.Contains(c))
                    {
                        components.Add(c);
                        c.Group = this;
                    }
                }
            }
        }

        private static System.Collections.Generic.List<SourceComponentState> GetGroupableComponents(object scene)
        {
            var tracker = ((ISourceScene)scene).SourceScene.Components;
            var type = tracker.Catalog.Find(typeof(GroupableMoveBlock));
            return tracker.GetTrackedComponents(type.Name);
        }

        public void Trigger()
        {
            foreach (GroupableMoveBlock component in components)
                if (component.State == GroupableMoveBlock.MovementState.Idling)
                    component.GroupTriggerSignal = true;
        }

        public bool CanRespawn(GroupableMoveBlock groupable)
        {
            switch (respawnBehavior)
            {
                case RespawnBehavior.Immediate:
                    return true;

                case RespawnBehavior.Simultaneous:
                    foreach (GroupableMoveBlock c in components)
                        if (!c.WaitingForRespawn)
                            return false;
                    return true;

                case RespawnBehavior.Sequential:
                    int componentIndex = components.IndexOf(groupable);
                    for (int i = 0; i < componentIndex; i++)
                        if (components[i].State != GroupableMoveBlock.MovementState.Idling)
                            return false;
                    return true;
            }

            return false;
        }

        public void Respawn()
        {
            if (respawnBehavior != RespawnBehavior.Simultaneous)
                return;

            foreach (GroupableMoveBlock component in components)
                component.WaitingForRespawn = false;
        }
    }
}
