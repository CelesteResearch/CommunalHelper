using Celeste.Graphics.Rendering.Drawing;
using Celeste.Formats.Map.Entities.Structure;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Rendering;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;

namespace Celeste.Mods.CommunalHelper.Entities.Chains
{
    public partial class Chain
    {
        private static readonly string sourcePlayerRegistryKey =
            typeof(global::Celeste.Simulation.RoomPlayerSession).FullName;

        private static SceneEntityState FirstSolid(object scene, IntRect rectangle)
        {
            foreach (var entry in ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities("Celeste.Solid"))
            {
                var entity = (SceneEntityState)entry;
                if (entity.Collidable && SourceEntityGeometryCollision.Rectangle(entity, rectangle)) return entity;
            }

            return null;
        }

        private bool TryGetPlayer(out RoomPlayerSession player)
        {
            player = (engine.Frame.Scenes.CurrentScene as ISourceScene)?.SourceScene.Entities
                .GetTrackedEntity<RoomPlayerSession>(sourcePlayerRegistryKey);
            return player != null;
        }

        private static void DrawOutlineOnlyCentered(ISourceDrawScope scope, AtlasEntry texture, Vector2 position,
            Vector2 scale, float rotation)
        {
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if (x != 0 || y != 0)
                    SourceAtlasDrawing.DrawCentered(scope, texture, position + new Vector2(x, y), PackedColor.Black,
                        scale, rotation);
        }
    }
}
