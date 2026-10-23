// SPDX-License-Identifier: MIT

using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Rectangle = Celeste.Core.Geometry.IntRect;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    [CustomEntity("CommunalHelper/SolidExtension")]
    [SceneTracked(false)]
    public class SolidExtension : SourceSolidEntityState
    {
        public bool HasGroup { get; private set; }

        public bool HasHitbox { get; private set; }

        public static SolidExtension Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
        {
            Vector2 at = data.Position + offset;
            return new SolidExtension(at, data.Width, data.Height, data.Bool("collidable", true),
                level.SolidWorldFactory.Create(at, false));
        }

        public SolidExtension(Vector2 position, int width, int height, bool collidable, ISourceSolidWorld world)
            : base(position, width, height, false, world)
        {
            HasHitbox = collidable;
        }

        public void AddToGroupAndFindChildren(SolidExtension from, ConnectedSolid master,
            List<SolidExtension> extensions)
        {
            from.HasGroup = true;
            extensions.Add(from);

            if (from.X < master.GroupBoundsMin.X)
                master.GroupBoundsMin.X = (int)from.X;

            if (from.Y < master.GroupBoundsMin.Y)
                master.GroupBoundsMin.Y = (int)from.Y;

            if (from.Right > master.GroupBoundsMax.X)
                master.GroupBoundsMax.X = (int)from.Right;

            if (from.Bottom > master.GroupBoundsMax.Y)
                master.GroupBoundsMax.Y = (int)from.Bottom;


            foreach (SolidExtension extention in ((ISourceScene)Scene).SourceScene.Entities
                     .GetTrackedEntities(typeof(SolidExtension)).Cast<SolidExtension>())
            {
                if (!extention.HasGroup &&
                    (SourceSceneCollision.CheckRect(
                         new Rectangle((int)from.X - 1, (int)from.Y, (int)from.Width + 2, (int)from.Height),
                         extention) ||
                     SourceSceneCollision.CheckRect(
                         new Rectangle((int)from.X, (int)from.Y - 1, (int)from.Width, (int)from.Height + 2),
                         extention) ||
                     SourceSceneCollision.CheckRect(
                         new Rectangle((int)master.X - 1, (int)master.Y, (int)master.Width + 2, (int)master.Height),
                         extention) ||
                     SourceSceneCollision.CheckRect(
                         new Rectangle((int)master.X, (int)master.Y - 1, (int)master.Width, (int)master.Height + 2),
                         extention)))
                {
                    AddToGroupAndFindChildren(extention, master, extensions);
                }
            }
        }

        public override void Update()
        {
            base.Update();
            if (!HasGroup)
                RemoveSelf();
        }
    }
}
