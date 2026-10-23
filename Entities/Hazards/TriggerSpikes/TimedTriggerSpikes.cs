using Celeste.Core.Display;
using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision.Blockers;
using Celeste.Simulation.Gameplay.Hazards.Spikes;
using Celeste.Simulation.Gameplay.Hazards.TriggerSpikes.Components;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display;
using Celeste.Graphics.Rendering;
using Celeste.Core.Reflection.Interop;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Mods.CommunalHelper.Runtime.Hazards;
using Directions = Celeste.Simulation.Gameplay.Hazards.TriggerSpikes.TriggerSpikesDirection;

namespace Celeste.Mods.CommunalHelper.Entities.Hazards.TriggerSpikes
{
    [CustomEntity(
        "CommunalHelper/TimedTriggerSpikesUp = LoadUp",
        "CommunalHelper/TimedTriggerSpikesDown = LoadDown",
        "CommunalHelper/TimedTriggerSpikesLeft = LoadLeft",
        "CommunalHelper/TimedTriggerSpikesRight = LoadRight")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.TimedTriggerSpikes")]
    public partial class TimedTriggerSpikes : SceneEntityState, ISourceEngineContextEntity
    {
        #region Loading

        public static SceneEntityState LoadUp(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData entityData)
        {
            return new TimedTriggerSpikes(entityData, offset, Directions.Up, level.EngineContext);
        }

        public static SceneEntityState LoadDown(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData entityData)
        {
            return new TimedTriggerSpikes(entityData, offset, Directions.Down, level.EngineContext);
        }

        public static SceneEntityState LoadLeft(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData entityData)
        {
            return new TimedTriggerSpikes(entityData, offset, Directions.Left, level.EngineContext);
        }

        public static SceneEntityState LoadRight(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData entityData)
        {
            return new TimedTriggerSpikes(entityData, offset, Directions.Right, level.EngineContext);
        }

        #endregion

        private readonly ISourceEngineContext engine;
        private readonly TimedSpikeCapabilities capabilities;
        ISourceEngineContext ISourceEngineContextEntity.EngineContext => engine;
        public PackedColor GetHue(object scene, Vector2 position) => capabilities.GetHue(scene, position);

        private const float RetractTime = 6f;

        protected float Delay = 0.4f;
        protected bool Triggered = false;

        private readonly bool waitForPlayer;

        private readonly int size;

        private readonly Directions direction;

        private readonly string overrideType;

        private Vector2 outwards;

        private Vector2 shakeOffset;

        private readonly bool grouped = false;
        private readonly bool rainbow = false;

        private string spikeType;
        private SpikeInfo[] spikes;
        private List<AtlasEntry> spikeTextures;

        private readonly bool triggerAlways;

        private readonly bool reusable;
        private readonly float reusableTimer;

        private bool legacyBehavior;

        public TimedTriggerSpikes(EntityData data, Vector2 offset, Directions dir, ISourceEngineContext engine)
            : this(
                data.Position, offset, GetSize(data, dir), dir,
                EntityAttributeReader.Attr(data.Values, "type", "default"),
                EntityAttributeReader.Float(data.Values, "Delay", 0.4f),
                EntityAttributeReader.Bool(data.Values, "WaitForPlayer", false),
                EntityAttributeReader.Bool(data.Values, "Grouped", false),
                EntityAttributeReader.Bool(data.Values, "Rainbow", false),
                EntityAttributeReader.Bool(data.Values, "TriggerAlways", false),
                EntityAttributeReader.Bool(data.Values, "Reusable"),
                EntityAttributeReader.Float(data.Values, "ReusableTimer"),
                EntityAttributeReader.Bool(data.Values, "legacyBehavior", true), engine
            )
        {
        }

        public TimedTriggerSpikes(
            Vector2 position, Vector2 offset, int size, Directions direction,
            string overrideType, float Delay, bool waitForPlayer,
            bool grouped, bool rainbow, bool triggerAlways,
            bool reusable, float reusableTimer, bool legacyBehavior, ISourceEngineContext engine
        )
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position + offset)
        {
            this.engine = engine;
            capabilities = TimedSpikeCapabilities.For(engine);
            if (grouped && !capabilities.HasModule("MaxHelpingHand"))
            {
                throw new Exception(
                    "Grouped Timed Trigger Spikes attempted to load without Maddie's Helping Hand as a dependency.");
            }

            if (rainbow && !capabilities.HasModule("VivHelper"))
            {
                throw new Exception(
                    "Rainbow Timed Trigger Spikes attempted to load without Viv's Helper as a dependency.");
            }

            this.size = size;
            this.direction = direction;
            this.overrideType = overrideType;
            this.Delay = Delay;
            this.waitForPlayer = waitForPlayer;
            this.grouped = grouped;
            this.rainbow = rainbow;
            this.triggerAlways = triggerAlways;
            this.reusable = reusable;
            this.reusableTimer = reusableTimer;
            this.legacyBehavior = legacyBehavior;

            SafeGroundBlockerComponentState safeGroundBlocker = null;
            LedgeBlockerState ledgeBlocker = null;

            switch (direction)
            {
                case Directions.Up:
                    outwards = new Vector2(0f, -1f);
                    Collider = new HitboxColliderGeometry(size, 3f, 0f, -3f);
                    Add(safeGroundBlocker = new SafeGroundBlockerComponentState());
                    Add(ledgeBlocker = new LedgeBlockerState(p => UpSafeBlockCheck((RoomPlayerSession)p)));
                    break;

                case Directions.Down:
                    outwards = new Vector2(0f, 1f);
                    Collider = new HitboxColliderGeometry(size, 3f);
                    // note: we set Blocking = false, set to true only using GravityHelper
                    Add(safeGroundBlocker = new SafeGroundBlockerComponentState() { Blocking = false });
                    Add(ledgeBlocker = new LedgeBlockerState(p => UpSafeBlockCheck((RoomPlayerSession)p))
                        { Blocking = false });
                    break;

                case Directions.Left:
                    outwards = new Vector2(-1f, 0f);
                    Collider = new HitboxColliderGeometry(3f, size, -3f);
                    Add(safeGroundBlocker = new SafeGroundBlockerComponentState());
                    Add(ledgeBlocker = new LedgeBlockerState(p => SideSafeBlockCheck((RoomPlayerSession)p)));
                    break;

                case Directions.Right:
                    outwards = new Vector2(1f, 0f);
                    Collider = new HitboxColliderGeometry(3f, size);
                    Add(safeGroundBlocker = new SafeGroundBlockerComponentState());
                    Add(ledgeBlocker = new LedgeBlockerState(p => SideSafeBlockCheck((RoomPlayerSession)p)));
                    break;
            }

            // GravityHelper listener to enable inverted ledge blocks & safe ground blockers
            SourceComponentState listener = capabilities.CreatePlayerGravityListener?.Invoke((_, value, _) =>
            {
                bool active = direction == Directions.Up ^ value == 1;
                safeGroundBlocker.Blocking = ledgeBlocker.Blocking = active;
            });
            if (listener is not null)
                Add(listener);

            Add(new PlayerColliderComponentState(OnCollide));
            Add(new SourceStaticMoverState
            {
                OnShake = OnShake,
                SolidChecker = IsRiding,
                JumpThruChecker = IsRiding
            });

            Depth = -50;
        }

        public override void Added(object scene)
        {
            base.Added(scene);

            spikeType = ((ISourceSpikeScene)scene).SpikeType;
            if (!string.IsNullOrEmpty(overrideType) && overrideType != "default")
            {
                spikeType = overrideType;
            }

            if (spikeType == "tentacles")
            {
                throw new NotSupportedException("Trigger tentacles currently not supported");
            }

            spikes = new SpikeInfo[size / 8];
            string str = direction.ToString().ToLower();
            spikeTextures =
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas.GetAtlasSubtextures("danger/spikes/" +
                    spikeType + "_" + str);
            for (int i = 0; i < spikes.Length; i++)
            {
                spikes[i].Parent = this;
                spikes[i].Index = i;
                spikes[i].Position = direction switch
                {
                    Directions.Up => (Vector2.UnitX * (i + 0.5f) * 8f) + Vector2.UnitY,
                    Directions.Down => (Vector2.UnitX * (i + 0.5f) * 8f) - Vector2.UnitY,
                    Directions.Left => (Vector2.UnitY * (i + 0.5f) * 8f) + Vector2.UnitX,
                    Directions.Right => (Vector2.UnitY * (i + 0.5f) * 8f) - Vector2.UnitX,
                    _ => throw new NotImplementedException(),
                };
                spikes[i].DelayTimer = Delay;
                spikes[i].ReusableTimer = reusableTimer;
                spikes[i].color = PackedColor.White;
            }
        }

        private void OnShake(Vector2 amount)
        {
            shakeOffset += amount;
        }

        private bool UpSafeBlockCheck(RoomPlayerSession player)
        {
            int dir = 8 * (int)player.Data.Facing;
            int left = (int)((player.Left + dir - Left) / 8f);
            int right = (int)((player.Right + dir - Left) / 8f);

            if (right < 0 || left >= spikes.Length)
                return false;

            left = Math.Max(left, 0);
            right = Math.Min(right, spikes.Length - 1);
            for (int i = left; i <= right; i++)
                if (spikes[i].Lerp >= 1f)
                    return true;

            return false;
        }

        private bool SideSafeBlockCheck(RoomPlayerSession player)
        {
            int top = (int)((player.Top - Top) / (legacyBehavior ? 4f : 8f));
            int bottom = (int)((player.Bottom - Top) / (legacyBehavior ? 4f : 8f));

            if (bottom < 0 || top >= spikes.Length)
                return false;

            top = Math.Max(top, 0);
            bottom = Math.Min(bottom, spikes.Length - 1);

            for (int i = top; i <= bottom; i++)
                if (spikes[i].Lerp >= 1f)
                    return true;

            return false;
        }

        private void OnCollide(RoomPlayerSession player)
        {
            GetPlayerCollideIndex(player, out int minIndex, out int maxIndex);
            if (maxIndex >= 0 && minIndex < spikes.Length)
            {
                minIndex = Math.Max(minIndex, 0);
                maxIndex = Math.Min(maxIndex, spikes.Length - 1);

                //attempt to breakout early if player dies
                bool breakout = false;
                for (int i = minIndex; i <= maxIndex; i++)
                {
                    // we need to flip the vertical speed if the player is inverted, just for the check
                    float ySpeed = player.Data.Motion.Normal.Speed.Y;
                    if (capabilities.IsPlayerInverted?.Invoke() ?? false)
                        ySpeed *= -1f;

                    //direction
                    switch (direction)
                    {
                        case Directions.Up:
                            if (ySpeed >= 0f || (!spikes[i].Triggered && triggerAlways))
                                breakout = !spikes[i].OnPlayer(player, outwards);
                            break;
                        case Directions.Down:
                            if (ySpeed <= 0f || (!spikes[i].Triggered && triggerAlways))
                                breakout = !spikes[i].OnPlayer(player, outwards);
                            break;
                        case Directions.Left:
                            if (player.Data.Motion.Normal.Speed.X >= 0f || (!spikes[i].Triggered && triggerAlways))
                                breakout = !spikes[i].OnPlayer(player, outwards);
                            break;
                        case Directions.Right:
                            if (player.Data.Motion.Normal.Speed.X <= 0f || (!spikes[i].Triggered && triggerAlways))
                                breakout = !spikes[i].OnPlayer(player, outwards);
                            break;
                    }

                    if (breakout)
                        break;
                }
            }
        }

        private void GetPlayerCollideIndex(RoomPlayerSession player, out int minIndex, out int maxIndex)
            => Celeste.Simulation.Gameplay.Hazards.TriggerSpikes.TriggerSpikeCollision.GetPlayerCollideIndices(player,
                this, direction, 8f, out minIndex, out maxIndex, triggerAlways);

        private bool PlayerCheck(int spikeIndex)
        {
            RoomPlayerSession player = FirstPlayer();
            if (player is null || !waitForPlayer)
            {
                return false;
            }

            GetPlayerCollideIndex(player, out int minIndex, out int maxIndex);
            return minIndex <= spikeIndex + 1 && maxIndex >= spikeIndex - 1;
        }

        private static int GetSize(EntityData data, Directions dir)
        {
            return dir <= Directions.Down ? data.Width : data.Height;
        }

        public override void Update()
        {
            base.Update();
            for (int i = 0; i < spikes.Length; i++)
            {
                spikes[i].Update();
            }

            playerPresent = FirstPlayer() is not null;
        }

        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);
            Vector2 justify = direction switch
            {
                Directions.Up => new Vector2(0.5f, 1f),
                Directions.Down => new Vector2(0.5f, 0f),
                Directions.Left => new Vector2(1f, 0.5f),
                Directions.Right => new Vector2(0f, 0.5f),
                _ => Vector2.One * 0.5f,
            };
            for (int i = 0; i < spikes.Length; i++)
            {
                AtlasEntry mTexture = spikeTextures[spikes[i].TextureIndex];
                Vector2 position = Position + shakeOffset + spikes[i].Position +
                                   (outwards * (-4f + (spikes[i].Lerp * 4f)));
                SourceAtlasDrawing.DrawJustified(context, this, mTexture, position, justify, spikes[i].color,
                    Vector2.One, 0f);
            }
        }

        private bool IsRiding(SourceSolidEntityState solid)
        {
            return direction switch
            {
                Directions.Up => CheckOutside(solid, Position + Vector2.UnitY),
                Directions.Down => CheckOutside(solid, Position - Vector2.UnitY),
                Directions.Left => CheckOutside(solid, Position + Vector2.UnitX),
                Directions.Right => CheckOutside(solid, Position - Vector2.UnitX),
                _ => false,
            };
        }

        private bool IsRiding(ISourceJumpThruEntity jumpThru)
        {
            return direction == Directions.Up && CheckAt(jumpThru, Position + Vector2.UnitY);
        }

        private bool playerPresent;
        private void PlaySound(string path, Vector2 at) => ((ISourceSpikeScene)Scene).PlaySpikeSound(path, at);

        private RoomPlayerSession FirstPlayer()
        {
            foreach (object entity in ((ISourceSpikeScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         ((Celeste.Scene.Tracking.ISourceSceneCatalogEnvironment)engine).SceneCatalog
                         .Find(typeof(RoomPlayerSession)).RegistryKey))

                if (SourceEntityCollision.Check(this, (ISourceEntityCollider)entity))
                    return (RoomPlayerSession)entity;
            return null;
        }

        private bool CheckAt(ISourceEntityCollider other, Vector2 at)
        {
            Vector2 previous = Position;
            Position = at;
            bool result = SourceEntityCollision.Check(this, other);
            Position = previous;
            return result;
        }

        private bool CheckOutside(ISourceEntityCollider other, Vector2 at)
            => !SourceEntityCollision.Check(this, other) && CheckAt(other, at);
    }
}
