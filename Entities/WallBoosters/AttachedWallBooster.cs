// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Entities.Platforms.WallBoosters;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Mods.CommunalHelper.Runtime.WallBoosters;
using Celeste.Physics.Collision;
using Celeste.Simulation.Gameplay.Environment.Core;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;

namespace Celeste.Mods.CommunalHelper.Entities.WallBoosters
{
    [CustomEntity("CommunalHelper/AttachedWallBooster")]
    [EverestTrackedAs(typeof(OriginalWallBoosterState))]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.AttachedWallBooster")]
    public class AttachedWallBooster : OriginalWallBoosterState
    {
        private enum CoreModeBehavior
        {
            ToggleDefaultHot,
            ToggleDefaultCold,
            AlwaysHot,
            AlwaysCold
        }

        public Vector2 Shake = Vector2.Zero;
        internal bool LegacyBoost;
        private readonly CoreModeBehavior coreModeBehavior;
        private readonly ISourceEngineContext engine;
        private readonly List<Sprite> tiles;
        internal ISourceEngineContext Owner => engine;

        public static AttachedWallBooster Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(level.EngineContext, data, offset);

        public AttachedWallBooster(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(engine, data.Position + offset, data.Height, EntityAttributeReader.Bool(data.Values, "left"),
                EntityAttributeReader.Bool(data.Values, "notCoreMode"))
        {
            this.engine = engine;
            tiles = WallBoosterFields.Tiles(this);
            Remove(OwnedComponents.Get<SourceStaticMoverState>());
            Add(new SourceStaticMoverState
            {
                OnShake = OnShake,
                SolidChecker = IsRiding,
                OnEnable = OnEnable,
                OnDisable = OnDisable
            });
            coreModeBehavior =
                EntityAttributeReader.Enum(data.Values, "coreModeBehavior", CoreModeBehavior.ToggleDefaultHot);
            if (coreModeBehavior is CoreModeBehavior.AlwaysHot or CoreModeBehavior.AlwaysCold)
                Remove(OwnedComponents.Get<CoreModeListenerState>());
            string spriteField = EntityAttributeReader.Attr(data.Values, "sprite", "");
            if (!string.IsNullOrEmpty(spriteField))
                BuildCustomSprite(EntityAttributeReader.Bool(data.Values, "left"), spriteField);
            int spriteOffset = EntityAttributeReader.Int(data.Values, "spriteOffset", 0);
            foreach (Sprite tile in tiles)
                tile.Position += Vector2.UnitX * spriteOffset;
        }

        public void SetColor(PackedColor color)
        {
            foreach (Sprite tile in tiles)
                tile.Color = color;
        }

        private void OnDisable()
        {
            SetColor(PackedColor.Gray);
            Collidable = false;
        }

        private void OnEnable()
        {
            SetColor(PackedColor.White);
            Collidable = true;
        }

        private bool IsRiding(SourceSolidEntityState solid)
            => Facing switch
            {
                1 => !SourceEntityCollision.Check(this, solid) &&
                     SourceEntityCollision.CheckAt(this, solid, Position + Vector2.UnitX),
                -1 => !SourceEntityCollision.Check(this, solid) &&
                      SourceEntityCollision.CheckAt(this, solid, Position - Vector2.UnitX),
                _ => false
            };

        private void OnShake(Vector2 amount) => Shake += amount;

        public override void Render(ISourceRenderContext context)
        {
            Vector2 p = Position;
            Position += Shake;
            base.Render(context);
            Position = p;
        }

        public override void Added(object scene)
        {
            WallBoosterFields.EntityAdded(this, scene);
            RoomCoreMode currentMode = ((ISourceCoreModeScene)scene).CoreMode;
            RoomCoreMode mode = coreModeBehavior switch
            {
                _ when WallBoosterFields.NotCoreMode(this) => RoomCoreMode.Cold,
                CoreModeBehavior.ToggleDefaultHot => currentMode == RoomCoreMode.Cold
                    ? RoomCoreMode.Cold
                    : RoomCoreMode.Hot,
                CoreModeBehavior.ToggleDefaultCold => currentMode == RoomCoreMode.Hot
                    ? RoomCoreMode.Hot
                    : RoomCoreMode.Cold,
                CoreModeBehavior.AlwaysHot => RoomCoreMode.Hot,
                CoreModeBehavior.AlwaysCold => RoomCoreMode.Cold,
                _ => throw new ArgumentOutOfRangeException()
            };
            WallBoosterFields.ChangeMode(this, mode);
        }

        private void BuildCustomSprite(bool left, string spriteField)
        {
            foreach (Sprite tile in tiles)
                Remove(tile);
            tiles.Clear();
            string top = spriteField + "Top";
            string mid = spriteField + "Mid";
            string bottom = spriteField + "Bottom";
            var graphics = ((ISourceGameGraphicsEnvironment)engine).GameGraphics;
            bool spritePathExists = graphics.SpriteFactory.Has(top) && graphics.SpriteFactory.Has(mid) &&
                                    graphics.SpriteFactory.Has(bottom);
            if (!spritePathExists && !spriteField.EndsWith("/", StringComparison.Ordinal))
                spriteField += "/";
            for (int i = 0; i < Height; i += 8)
            {
                Sprite sprite;
                if (spritePathExists)
                {
                    string id = i == 0 ? top : i + 16 > Height ? bottom : mid;
                    sprite = graphics.SpriteFactory.Create(id);
                }
                else
                {
                    string id = i == 0 ? "Top" : i + 16 > Height ? "Bottom" : "Mid";
                    sprite = new Sprite(engine, graphics.GameAtlas, spriteField);
                    sprite.AddLoop("hot", "fire" + id, .035f, 0, 1, 2, 3, 4, 5, 6, 7);
                    sprite.Add("ice", "ice" + id, .08f, "ice,ice,ice,ice,ice,iceShine", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                    sprite.Add("iceShine", "ice" + id, .08f, "ice", 0, 1, 2, 3, 4);
                }

                if (!left)
                {
                    sprite.FlipX = true;
                    sprite.Position = new Vector2(4f, i);
                }
                else
                    sprite.Position = new Vector2(0f, i);

                tiles.Add(sprite);
                Add(sprite);
            }
        }
    }
}
