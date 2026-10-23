// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Components.Animation;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Scene.Scheduling.ExecutionTags;
using Celeste.Mods.CommunalHelper.Runtime.Panels;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Directions = Celeste.Simulation.Gameplay.Hazards.Spikes.SpikeDirection;

namespace Celeste.Mods.CommunalHelper.Entities.Panels
{
    [SceneTracked(true)]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.AbstractPanel")]
    public abstract class AbstractPanel : SceneEntityState
    {
        protected readonly ISourceEngineContext engine;
        protected readonly PanelStaticMover staticMover;
        private readonly bool overrideAllowStaticMovers;
        protected Vector2 platformShake;
        protected OriginalLevelSceneRoot level;
        public Directions Orientation;
        public float Alpha = 1f;

        public Vector2 Start => new(X + (Orientation is Directions.Right or Directions.Down ? Width : 0),
            Y + (Orientation is Directions.Left or Directions.Down ? Height : 0));

        public Vector2 End => new(X + (Orientation is Directions.Up or Directions.Right ? Width : 0),
            Y + (Orientation is Directions.Right or Directions.Down ? Height : 0));

        protected static int GetSize(EntityData data, Directions dir) =>
            dir <= Directions.Down ? data.Width : data.Height;

        protected AbstractPanel(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, data.Position + offset,
                GetSize(data, EntityAttributeReader.Enum<Directions>(data.Values, "orientation")),
                EntityAttributeReader.Enum<Directions>(data.Values, "orientation"),
                EntityAttributeReader.Bool(data.Values, "overrideAllowStaticMovers"))
        {
        }

        protected AbstractPanel(ISourceEngineContext engine, Vector2 position, float size, Directions orientation,
            bool overrideAllowStaticMovers)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            this.engine = engine;
            Depth = -13000;
            Orientation = orientation;
            this.overrideAllowStaticMovers = overrideAllowStaticMovers;
            Collider = orientation switch
            {
                Directions.Up or Directions.Down => new HitboxColliderGeometry(size, 8f),
                Directions.Left or Directions.Right => new HitboxColliderGeometry(8f, size),
                _ => null
            };
            Add(staticMover = new PanelStaticMover
            {
                OnAttach = platform => OnAttach((SourcePlatformEntityState)platform),
                OnShake = v => platformShake += v,
                SolidChecker = IsRiding,
                OnEnable = () => Active = Visible = Collidable = true,
                OnDisable = () => Active = Visible = Collidable = false,
                OnDestroy = Destroy
            });
        }

        protected virtual void OnAttach(SourcePlatformEntityState platform)
        {
        }

        private bool IsRiding(SourceSolidEntityState solid)
        {
            Vector2 at = Orientation switch
            {
                Directions.Up => TopCenter - Vector2.UnitY * Height,
                Directions.Down => BottomCenter + Vector2.UnitY,
                Directions.Left => CenterLeft - Vector2.UnitX * Width,
                Directions.Right => CenterRight + Vector2.UnitX,
                _ => Position
            };
            return Orientation is >= Directions.Up and <= Directions.Right &&
                   SourceEntityCollision.Check(this, solid) &&
                   !SourceEntityCollision.CheckAt(this, solid, at);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            level = scene as OriginalLevelSceneRoot;
        }

        internal int PersistentMask =>
            ((ISourceGlobalFrozenExecutionTagsEnvironment)engine).GlobalFrozenExecutionTags.Global.Value |
            ((ISourcePersistentTransitionExecutionTagsEnvironment)engine).PersistentTransitionExecutionTags.Persistent
            .Value;

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (!overrideAllowStaticMovers || staticMover.Platform != null) return;
            var entities = ((ISourceScene)scene).SourceScene.Entities.Entities;
            int mask = PersistentMask;
            for (int i = 0; i < entities.Count; i++)
                if (entities[i] is SourceSolidEntityState solid && (solid.Tags.Bits & mask) != 0 &&
                    ReferenceEquals(solid.Scene, scene))
                    ForceAttachStaticMovers(solid, scene);
        }

        internal static void ForceAttachStaticMovers(SourceSolidEntityState solid, object scene)
        {
            bool collidable = solid.Collidable;
            solid.Collidable = true;
            var panels = ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities(typeof(AbstractPanel));
            for (int i = 0; i < panels.Count; i++)
            {
                var panel = (AbstractPanel)panels[i];
                if (panel.overrideAllowStaticMovers && panel.staticMover.Platform == null &&
                    panel.staticMover.IsRiding(solid))
                {
                    panel.staticMover.AttachForced(solid);
                    panel.staticMover.OnAttach?.Invoke(solid);
                }
            }

            solid.Collidable = collidable;
        }

        protected virtual void Destroy()
        {
            Collidable = false;
            var tween = SourceTweenState.Create(engine.TweenPool, SourceTweenState.TweenMode.Oneshot, t => t, 1f, true);
            tween.OnUpdate = t => Alpha = 1f - t.Percent;
            tween.OnComplete = _ => RemoveSelf();
            Add(tween);
        }

        public override void Update()
        {
            if (staticMover.Platform == null)
            {
                RemoveSelf();
                return;
            }

            base.Update();
        }
    }
}
