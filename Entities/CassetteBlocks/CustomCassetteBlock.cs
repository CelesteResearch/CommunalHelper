// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Maps;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Core.Rendering;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Game.Gameplay.Interaction.Springs;
using Celeste.Game.Presentation.Platforms.Cassette;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Hazards.Spikes;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Mods.CommunalHelper.Presentation.Colors;
using Celeste.Mods.CommunalHelper.Runtime.CassetteBlocks;
using Color = Celeste.Core.Display.PackedColor;
using Image = Celeste.Graphics.Animation.SpriteImage;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Rectangle = Celeste.Core.Geometry.IntRect;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    [EverestTrackedAs(typeof(CassetteBlockState), true)]
    [CustomEntity("CommunalHelper/CustomCassetteBlock")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CustomCassetteBlock")]
    public partial class CustomCassetteBlock : CassetteBlockState, ISourceCassetteBlockSidePresentation
    {
        protected Color[] colorOptions =
        [
            SourceHexColor.HexToColor("49aaf0"),
            SourceHexColor.HexToColor("f049be"),
            SourceHexColor.HexToColor("fcdc3a"),
            SourceHexColor.HexToColor("38e04e")
        ];

        protected new Color color;
        protected Color pressedColor;

        protected int BlockHeight
        {
            get => blockHeight;
            set => blockHeight = value;
        }

        /// <summary>
        /// Block offset based on <c>(2 - <see cref="BlockHeight"/>)</c>
        /// </summary>
        protected Vector2 BlockOffset => Vector2.UnitY * (2 - BlockHeight);

        private readonly bool dynamicHitbox;
        private readonly string playerTrackerKey;
        private readonly Hitbox[] hitboxes;

        private bool present = true;

        /// <summary>
        /// Whether the block is actually collidable, not just according to cassette state.
        /// </summary>
        public bool Present
        {
            get => present;
            set
            {
                present = value;
                // Update collision immediately without waiting for Update
                Collidable = value && virtualCollidable;
            }
        }

        // Whether the block is collidable according to cassette state
        private bool virtualCollidable = true;

        public bool Held { get; set; }


        // If true, this cassette block will not connect to any others
        private bool lonely = false;

        private readonly bool oldConnectionBehavior = false;

        protected bool Lonely
        {
            get { return !oldConnectionBehavior && lonely; }

            set { lonely = value; }
        }

        protected readonly string spritePath;
        protected readonly float sideAlpha;

        protected string SuffixFromSpritePathOrDefault(string suffix, string defaultValue)
            => string.IsNullOrEmpty(spritePath) ? defaultValue : spritePath + suffix;

        public CustomCassetteBlock(EntityData data, Vector2 offset, MapEntityId id, ISourceSolidWorldFactory worlds)
            : this(data.Position + offset, id, data.Width, data.Height, data.Int("index"), data.Float("tempo", 1f),
                false,
                data.Bool("oldConnectionBehavior", true), false, HexColorNullable(data, "customColor"),
                data.Attr("spritePath", ""), data.Float("sideAlpha", 1f), data.Bool("held"),
                worlds.Create(data.Position + offset, false))
        {
        }

        public CustomCassetteBlock(Vector2 position, MapEntityId id, int width, int height, int index, float tempo,
            bool lonely, bool oldConnectionBehavior, bool dynamicHitbox, Color? overrideColor, string spritePath,
            float sideAlpha, bool held, ISourceSolidWorld world)
            : base(position, id, width, height, index, tempo, world)
        {
            playerTrackerKey = ((ISourceSceneCatalogEnvironment)EngineContext).SceneCatalog
                .Find(typeof(RoomPlayerSession))
                .RegistryKey;
            Held = held;
            Index = index;
            color = overrideColor ?? colorOptions[index];
            pressedColor = CommunalColorChannels.Multiply(color, SourceHexColor.HexToColor("667da5"));

            base.color = color;

            this.dynamicHitbox = dynamicHitbox;
            if (dynamicHitbox)
            {
                hitboxes = new Hitbox[3];
                hitboxes[0] = new Hitbox(Collider.Width, Collider.Height - 2);
                hitboxes[1] = new Hitbox(Collider.Width, Collider.Height - 1);
                hitboxes[2] = Collider as Hitbox;
            }

            this.oldConnectionBehavior = oldConnectionBehavior;
            Lonely = lonely;

            this.spritePath = spritePath;
            this.sideAlpha = sideAlpha;
        }

        public override void Update()
        {
            if (!Present)
            {
                Collidable = virtualCollidable;
            }

            base.Update();
            // Update what the cassette state dictates collision should be
            virtualCollidable = Collidable;

            if (!Present)
            {
                Collidable = false;
                DisableStaticMovers();
            }
        }

        protected void AddCenterSymbol(Image solid, Image pressed)
        {
            this.solid.Add(solid);
            this.pressed.Add(pressed);
            List<Image> all = this.all;
            Vector2 origin = groupOrigin - Position;
            Vector2 size = new(Width, Height);

            Vector2 half = (size - new Vector2(solid.Width, solid.Height)) * 0.5f;
            solid.Origin = origin - half;
            solid.Position = origin;
            solid.Color = color;
            Add(solid);
            all.Add(solid);

            half = (size - new Vector2(pressed.Width, pressed.Height)) * 0.5f;
            pressed.Origin = origin - half;
            pressed.Position = origin;
            pressed.Color = color;
            Add(pressed);
            all.Add(pressed);
        }

        public void HandleShiftSize(int amount)
        {
            if (dynamicHitbox)
                Collider = hitboxes[BlockHeight - amount];
        }

        public virtual void HandleUpdateVisualState()
        {
            side.Visible &= Visible;
            foreach (SourceStaticMoverState staticMover in staticMovers)
            {
                staticMover.Visible = Visible;
            }
        }

        /// <summary>
        /// Makes static movers (Spikes and Springs) appear or disappear when they are disabled.
        /// </summary>
        /// <param name="visible">Whether disabled static movers should be visible.</param>
        protected void SetDisabledStaticMoversVisibility(bool visible)
        {
            foreach (SourceStaticMoverState staticMover in staticMovers)
            {
                if (staticMover.Entity is SpikesState spikes)
                    spikes.VisibleWhenDisabled = visible;

                if (staticMover.Entity is OriginalSpringState spring)
                    spring.VisibleWhenDisabled = visible;
            }
        }

        protected void SetStaticMoversVisible(bool visible)
        {
            foreach (SourceStaticMoverState staticMover in staticMovers)
                ((SceneEntityState)staticMover.Entity).Visible = visible;
        }

        internal ISourceEngineContext EngineContext => SourceWorld.EngineContext;
        protected AtlasData GameAtlas => ((ISourceGameGraphicsEnvironment)EngineContext).GameGraphics.GameAtlas;

        protected static Color? HexColorNullable(EntityData data, string key)
        {
            string text = data.Attr(key);
            return text.Length is 6 or 7 ? SourceHexColor.HexToColor(text) : null;
        }

        public static CustomCassetteBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
        {
            return new(data, offset, new MapEntityId(room.Name, data.ID), level.SolidWorldFactory);
        }

        protected override void ShiftSize(int amount)
        {
            if (Activated && CollideCheck(playerTrackerKey)) amount *= -1;
            int newHeight = BlockHeight - amount;
            if (newHeight is > 2 or < 0) return;
            HandleShiftSize(amount);
            base.ShiftSize(amount);
        }

        protected override void UpdateVisualState()
        {
            base.UpdateVisualState();
            HandleUpdateVisualState();
        }

        public void RenderBoxSide(ISourceRenderContext context, object actualSide, Color disabledColor)
            => SourcePrimitiveDrawing.Rect(context, actualSide, X, Y + Height - 8f, Width, 8 + BlockHeight,
                disabledColor * sideAlpha);
    }
}
