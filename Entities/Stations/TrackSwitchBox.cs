using Celeste.Graphics.Rendering.Drawing;
// SPDX-License-Identifier: MIT
using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Gameplay.Collision;
using Celeste.Physics.Collision;
using Celeste.Mods.CommunalHelper.Content.Stations;
using Celeste.Mods.CommunalHelper.Runtime.Hazards;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using SoundSource = Celeste.Simulation.Audio.Sources.SoundSourceComponentState;
using Player = Celeste.Simulation.RoomPlayerSession;
using DashCollisionResults = Celeste.Simulation.Gameplay.Player.Collision.Surfaces.DashCollisionResult;
using Node = Celeste.Mods.CommunalHelper.Entities.Stations.StationBlockTrack.Node;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Components.Shaking;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Game.Gameplay.Hazards.Lightning;
using Celeste.Game.Entities.PlaybackBillboard;
using static Celeste.Mods.CommunalHelper.Entities.Stations.StationBlockTrack;
using SineWave = Celeste.Scene.Components.Oscillation.SineWaveState;
using Shaker = Celeste.Scene.Components.Shaking.ShakerComponentState;
using Wiggler = Celeste.Scene.Components.Oscillation.WigglerComponent;
using Coroutine = Celeste.Core.Scheduling.SimulationCoroutine;
using Rectangle = Celeste.Core.Geometry.IntRect;

namespace Celeste.Mods.CommunalHelper.Entities.Stations
{
    [CustomEntity("CommunalHelper/TrackSwitchBox")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.TrackSwitchBox")]
    public partial class TrackSwitchBox : SourceSolidEntityState
    {
        private uint Seed;

        private ParticleType P_Smash => StationResources.For(Engine).P_Smash;

        private float circleRadius = 0f, circleOpacity = 0f;

        private readonly Sprite sprite;
        private readonly SineWave sine;
        private Vector2 start;

        private float sink;
        private bool canSwitch = true;
        private readonly bool canFloat = true;
        private readonly bool canBounce = true;
        private readonly bool reverse = false;
        private readonly string switchFlag = "";

        private float shakeCounter;
        private bool smashParticles = false;

        private Vector2 bounceDir;
        private readonly Wiggler bounce;
        private readonly Shaker shaker;

        private Coroutine pulseRoutine;

        private readonly SoundSource Sfx;

        private TrackSwitchState LocalTrackSwitchState
        {
            get => StationResources.For(Engine).LocalTrackSwitchState;
            set => StationResources.For(Engine).LocalTrackSwitchState = value;
        }

        public static readonly Color OnColor = SourceHexColor.HexToColor("318eeb");
        public static readonly Color OffColor = SourceHexColor.HexToColor("e03a69");
        private float colorLerp;

        private readonly bool global = false;

        private bool spikesLeft, spikesRight, spikesUp, spikesDown;

        public TrackSwitchBox(ISourceEngineContext engine, Vector2 position, bool global, bool canFloat, bool canBounce,
            bool reverse, string flag, ISourceSolidWorldFactory worlds)
            : base(position, 32f, 32f, true, worlds.Create(position, true))
        {
            colorLerp = (LocalTrackSwitchState = StationResources.For(Engine).Session.TrackInitialState) ==
                        TrackSwitchState.On
                ? 0f
                : 1f;

            this.global = global;
            this.canFloat = canFloat;
            this.canBounce = canBounce;
            this.reverse = reverse;
            this.switchFlag = flag;

            SurfaceSoundIndex = 7;
            start = Position;
            sprite = StationResources.For(Engine).SpriteBank.Create("trackSwitchBox");
            sprite.Position = new Vector2(Width, Height) / 2f;
            sprite.OnLastFrame = anim =>
            {
                if (anim == "switch")
                    canSwitch = true;
            };

            OwnedComponents.Add(Sfx = new SoundSource(Engine)
            {
                Position = new Vector2(Width / 2, Height / 2)
            });

            OwnedComponents.Add(sprite);
            OwnedComponents.Add(sine = new SineWave(Engine, 0.5f, 0f));
            bounce = ((ISourceWigglerPoolScope)Engine).WigglerPool.Create(1f, 0.5f);
            bounce.StartZero = false;
            OwnedComponents.Add(bounce);
            OwnedComponents.Add(shaker = new Shaker(Engine, on: false));
            OnDashCollide = Dashed;
        }

        public TrackSwitchBox(ISourceEngineContext engine, EntityData e, Vector2 levelOffset,
            ISourceSolidWorldFactory worlds)
            : this(engine, e.Position + levelOffset, e.Bool("globalSwitch"), e.Bool("floaty"), e.Bool("bouncy"),
                e.Bool("reverse"), e.Attr("switchFlag"), worlds)
        {
        }

        public static TrackSwitchBox Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(level.EngineContext, data, offset, level.SolidWorldFactory);

        private ISourceEngineContext Engine => SourceWorld.EngineContext;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        public override void Awake(object scene)
        {
            base.Awake(scene);
            OriginalLevelSceneRoot level = Level;
            spikesUp = SourceSceneCollisionQueries.CheckAt(this,
                Level.Scheduler.Entities.GetTrackedEntities("Celeste.Spikes"), Position - Vector2.UnitY);
            spikesDown = SourceSceneCollisionQueries.CheckAt(this,
                Level.Scheduler.Entities.GetTrackedEntities("Celeste.Spikes"), Position + Vector2.UnitY);
            spikesLeft = SourceSceneCollisionQueries.CheckAt(this,
                Level.Scheduler.Entities.GetTrackedEntities("Celeste.Spikes"), Position - Vector2.UnitX);
            spikesRight = SourceSceneCollisionQueries.CheckAt(this,
                Level.Scheduler.Entities.GetTrackedEntities("Celeste.Spikes"), Position + Vector2.UnitX);
            if (LocalTrackSwitchState == TrackSwitchState.On)
            {
                level.Session.SetFlag(switchFlag, true);
            }

            if (LocalTrackSwitchState == TrackSwitchState.Off)
            {
                level.Session.SetFlag(switchFlag, false);
            }
        }

        public DashCollisionResults Dashed(Player player, Vector2 dir)
        {
            if (!((IOriginalGameplayProcess)Engine).SaveContext.CurrentSave.Assists.Invincible)
            {
                if (dir == Vector2.UnitX && spikesLeft) return DashCollisionResults.NormalCollision;
                if (dir == -Vector2.UnitX && spikesRight) return DashCollisionResults.NormalCollision;
                if (dir == Vector2.UnitY && spikesUp) return DashCollisionResults.NormalCollision;
                if (dir == -Vector2.UnitY && spikesDown) return DashCollisionResults.NormalCollision;
            }

            if (canSwitch)
            {
                Sfx.Play("event:/CommunalHelperEvents/game/trackSwitchBox/smash", "global_switch", global ? 1f : 0f);
                Level.DirectionalShake(dir);
                sprite.Scale = new Vector2(1f + (Math.Abs(dir.Y) * 0.4f) - (Math.Abs(dir.X) * 0.4f),
                    1f + (Math.Abs(dir.X) * 0.4f) - (Math.Abs(dir.Y) * 0.4f));
                shakeCounter = 0.2f;
                shaker.On = true;
                bounceDir = dir;
                bounce.Start();
                smashParticles = true;
                Pulse();
                OwnedComponents.Add(new Coroutine(Engine, SwitchSequence()));
                ((ISourceRumbleEnvironment)Engine).RumbleSession.Rumble((int)RumbleStrength.Medium,
                    (int)RumbleLength.Medium);
                canSwitch = false;
                if (reverse)
                {
                    Reverse(Scene, global);
                }
                else
                {
                    Switch(Scene,
                        LocalTrackSwitchState switch
                        {
                            TrackSwitchState.On => TrackSwitchState.Off, TrackSwitchState.Off => TrackSwitchState.On,
                            _ => throw new NotImplementedException()
                        }, global);
                }

                return DashCollisionResults.Rebound;
            }

            return DashCollisionResults.NormalCollision;
        }

        public static bool Switch(object scene, TrackSwitchState state, bool global = false)
        {
            var resources = StationResources.For(((ISourceEngineContextScene)scene).EngineContext);
            if (state == resources.LocalTrackSwitchState)
                return false;
            resources.LocalTrackSwitchState = state;
            SwitchTracks(scene, state);
            if (global)
                resources.Session.TrackInitialState = state;
            return true;
        }

        public static void Reverse(object scene, bool global = false)
        {
            ReverseTracks(scene);
            if (global)
            {
                var resources = StationResources.For(((ISourceEngineContextScene)scene).EngineContext);
                resources.Session.TrackInitialMoveMode = Invert(resources.Session.TrackInitialMoveMode);
            }
        }

        private void SmashParticles(Vector2 dir)
        {
            float direction;
            Vector2 position;
            Vector2 positionRange;
            int num;
            if (dir == Vector2.UnitX)
            {
                direction = 0f;
                position = CenterRight - (Vector2.UnitX * 12f);
                positionRange = Vector2.UnitY * (Height - 6f) * 0.5f;
                num = (int)(Height / 8f) * 4;
            }
            else if (dir == -Vector2.UnitX)
            {
                direction = (float)Math.PI;
                position = CenterLeft + (Vector2.UnitX * 12f);
                positionRange = Vector2.UnitY * (Height - 6f) * 0.5f;
                num = (int)(Height / 8f) * 4;
            }
            else if (dir == Vector2.UnitY)
            {
                direction = (float)Math.PI / 2f;
                position = BottomCenter - (Vector2.UnitY * 12f);
                positionRange = Vector2.UnitX * (Width - 6f) * 0.5f;
                num = (int)(Width / 8f) * 4;
            }
            else
            {
                direction = -(float)Math.PI / 2f;
                position = TopCenter + (Vector2.UnitY * 12f);
                positionRange = Vector2.UnitX * (Width - 6f) * 0.5f;
                num = (int)(Width / 8f) * 4;
            }

            num += 2;
            Level.Particles.Emit(P_Smash, num, position, positionRange, direction);
        }

        public override void Update()
        {
            base.Update();
            OriginalLevelSceneRoot level = Level;
            if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.1f, Engine))
            {
                Seed++;
            }

            colorLerp = ScalarMath.Approach(colorLerp, LocalTrackSwitchState == TrackSwitchState.On ? 0f : 1f,
                Engine.DeltaTime);
            circleOpacity = ScalarMath.Approach(circleOpacity, 0f, Engine.DeltaTime);
            circleRadius += (64 - circleRadius) / 4 * Engine.DeltaTime * 20f;

            if (shakeCounter > 0f)
            {
                shakeCounter -= Engine.DeltaTime;
                if (shakeCounter <= 0f)
                {
                    shaker.On = false;
                    sprite.Scale = Vector2.One * 1.2f;
                    sprite.Play("switch");

                    if (LocalTrackSwitchState == TrackSwitchState.On)
                        level.Session.SetFlag(switchFlag, true);

                    if (LocalTrackSwitchState == TrackSwitchState.Off)
                        level.Session.SetFlag(switchFlag, false);
                }
            }

            if (Collidable)
            {
                bool flag = HasPlayerRider();
                sink = ScalarMath.Approach(sink, flag ? 1 : 0, 2f * Engine.DeltaTime);
                sine.Rate = ((ISourceScalarEnvironment)Engine).ScalarArithmetic.Lerp(1f, 0.5f, sink);
                Vector2 vector = start;
                if (canFloat)
                {
                    vector.Y += (sink * 6f) +
                                (sine.Value * ((ISourceScalarEnvironment)Engine).ScalarArithmetic.Lerp(4f, 2f, sink));
                }

                if (canBounce)
                {
                    vector += bounce.Value * bounceDir * 12f;
                }

                MoveToX(vector.X);
                MoveToY(vector.Y);
                if (smashParticles)
                {
                    smashParticles = false;
                    SmashParticles(Vector2Geometry.Perpendicular(bounceDir));
                    SmashParticles(-Vector2Geometry.Perpendicular(bounceDir));
                }
            }

            sprite.Scale.X = ScalarMath.Approach(sprite.Scale.X, 1f, Engine.DeltaTime * 4f);
            sprite.Scale.Y = ScalarMath.Approach(sprite.Scale.Y, 1f, Engine.DeltaTime * 4f);
            LiftSpeed = Vector2.Zero;
        }

        public override void Render(ISourceRenderContext context)
        {
            SourcePrimitiveDrawing.Circle(context, this, Center, circleRadius, Color.White * circleOpacity, 10,
                ((ISourceVectorEnvironment)Engine).VectorArithmetic);
            Vector2 position = sprite.Position;
            sprite.Position += shaker.Value;

            Rectangle rect = new(
                (int)(Center.X + ((X - Center.X) * sprite.Scale.X)),
                (int)(Center.Y + ((Y - Center.Y) * sprite.Scale.Y)),
                (int)(Width * sprite.Scale.X),
                (int)(Height * sprite.Scale.Y));
            rect.Inflate(-1, -1);

            uint seed = Seed;

            SourcePrimitiveDrawing.Rect((ISourceDrawScope)context, rect, MixColor(OnColor, OffColor, colorLerp));

            for (int i = rect.Y; (float)i < rect.Bottom; i += 2)
            {
                float scale = 0.05f +
                              ((1f + (float)Math.Sin((i / 16f) +
                                                     (((ISourceScene)Scene).SourceScene.Clock.TimeActive * 2f))) / 2f *
                               0.2f);
                SourcePrimitiveDrawing.Line(context, this, new Vector2(rect.X, i), new Vector2(rect.X + rect.Width, i),
                    Color.White * 0.55f * scale, ((ISourceVectorEnvironment)Engine).VectorArithmetic);
            }

            PlaybackBillboardState.DrawNoise(context, rect, ref seed, Color.White * 0.1f,
                ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic);

            base.Render(context);
            sprite.Position = position;
        }

        private Color MixColor(Color a, Color b, float t)
        {
            t = ScalarMath.Clamp(t, 0f, 1f);
            var m = ((ISourceScalarEnvironment)Engine).ScalarArithmetic;
            return new Color((int)m.Lerp(a.R, b.R, t), (int)m.Lerp(a.G, b.G, t), (int)m.Lerp(a.B, b.B, t),
                (int)m.Lerp(a.A, b.A, t));
        }

        private void Pulse()
        {
            pulseRoutine = new Coroutine(Engine, LightningSourceRoutines.PulseRoutine(Level));
            OwnedComponents.Add(pulseRoutine);
        }

        private IEnumerator SwitchSequence()
        {
            yield return 0.8f;
            Level.DirectionalShake(Vector2.UnitX, 0.2f);
            ((ISourceRumbleEnvironment)Engine).RumbleSession.Rumble((int)RumbleStrength.Medium,
                (int)RumbleLength.Short);
            if (global)
            {
                circleRadius = 0f;
                circleOpacity = .5f;
            }
        }
    }
}
