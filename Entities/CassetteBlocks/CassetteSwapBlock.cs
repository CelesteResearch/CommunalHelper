// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Framework;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Entities.Platforms.SwapBlocks.Motion;
using Celeste.Game.Entities.Platforms.SwapBlocks.Rendering;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.CommunalHelper.Presentation.Colors;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Dash;
using Color = Celeste.Core.Display.PackedColor;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using Rectangle = Celeste.Core.Geometry.IntRect;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    [CustomEntity("CommunalHelper/CassetteSwapBlock")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CassetteSwapBlock")]
    public partial class CassetteSwapBlock : CustomCassetteBlock
    {
        private ISourceEngineContext Engine => SourceWorld.EngineContext;

        private static Rectangle CameraBounds(OriginalLevelSceneRoot level) => new((int)level.Camera.Left,
            (int)level.Camera.Top, (int)level.Camera.Right - (int)level.Camera.Left,
            (int)level.Camera.Bottom - (int)level.Camera.Top);

        public new static CassetteSwapBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset,
            OriginalLevelEntityData data)
            => new(data, offset, new MapEntityId(room.Name, data.ID), level.SolidWorldFactory);

        [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CassetteSwapBlock+PathRenderer")]
        private class PathRenderer : SceneEntityState
        {
            private readonly CassetteSwapBlock block;
            private Color pathColor;
            private Color pathColorPressed;

            public PathRenderer(CassetteSwapBlock block)
                : base(((ISourceEntitySourceDataEnvironment)block.Engine).EntitySourceDataRegistration, block.Position)
            {
                this.block = block;
                Depth = 9000;
                pathColor = CommunalColorChannels.Multiply(block.color, SourceHexColor.HexToColor("e0e7ea"));
                pathColorPressed =
                    CommunalColorChannels.Multiply(block.pressedColor, SourceHexColor.HexToColor("e0e7ea"));
            }

            public override void Update()
            {
                base.Update();
                Depth = block.Collidable ? 9000 : 9010;
            }

            public override void Render(ISourceRenderContext context)
            {
                Vector2 position = new Vector2(block.moveRect.X, block.moveRect.Y) + block.BlockOffset;
                for (int i = 1; i <= block.BlockHeight; ++i)
                    DrawTarget(context, position + (Vector2.UnitY * i), pathColorPressed * block.sideAlpha);
                DrawTarget(context, position, block.Collidable ? pathColor : pathColorPressed);
            }

            private void DrawTarget(ISourceRenderContext context, Vector2 position, Color color)
            {
                SwapBlockDrawing.DrawBlockStyleClipped(context, this, CameraBounds((OriginalLevelSceneRoot)Scene),
                    position,
                    block.moveRect.Width, block.moveRect.Height, block.nineSliceTarget, null, color);
            }
        }

        private const float ReturnTime = 0.8f;
        public Vector2 Direction;
        public bool Swapping;

        private Vector2 start;
        private Vector2 end;
        private float lerp;
        private int target;
        private Rectangle moveRect;

        private float speed;
        private readonly float maxForwardSpeed;
        private readonly float maxBackwardSpeed;
        private float returnTimer;

        private readonly MTexture[,] nineSliceTarget;

        private ISourceAudioEventReference moveSfx;
        private ISourceAudioEventReference returnSfx;
        private float particlesRemainder;
        private readonly ParticleType moveParticle;
        private readonly ParticleType moveParticlePressed;

        private readonly bool noReturn;

        public CassetteSwapBlock(Vector2 position, MapEntityId id, int width, int height, Vector2 node, int index,
            float tempo, bool oldConnectionBehavior, bool noReturn, Color? overrideColor, string spritePath,
            float sideAlpha, bool held, ISourceSolidWorld world)
            : base(position, id, width, height, index, tempo, true, oldConnectionBehavior, false, overrideColor,
                spritePath,
                sideAlpha, held, world)
        {
            start = Position;
            end = node;
            this.noReturn = noReturn;
            maxForwardSpeed = 360f / ((ISourceVectorEnvironment)Engine).VectorArithmetic.Distance(start, end);
            maxBackwardSpeed = maxForwardSpeed * 0.4f;
            Direction.X = Math.Sign(end.X - start.X);
            Direction.Y = Math.Sign(end.Y - start.Y);

            OwnedComponents.Add(new DashListenerState { OnDash = OnDash });

            int minX = (int)SourceFrameworkMath.Min(X, node.X);
            int minY = (int)SourceFrameworkMath.Min(Y, node.Y);
            int maxX = (int)SourceFrameworkMath.Max(X + Width, node.X + Width);
            int maxY = (int)SourceFrameworkMath.Max(Y + Height, node.Y + Height);
            moveRect = new Rectangle(minX, minY, maxX - minX, maxY - minY);

            MTexture mTexture3 = GameAtlas[SuffixFromSpritePathOrDefault("/target", "objects/swapblock/target")];
            nineSliceTarget = new MTexture[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    nineSliceTarget[i, j] = mTexture3.GetSubtexture(new Rectangle(i * 8, j * 8, 8, 8));
                }
            }

            moveParticle = new ParticleType(((IOriginalGameplayProcess)Engine).ParticleTypes.SwapBlock.P_Move)
            {
                Color = color,
                Color2 = color
            };
            moveParticlePressed = new ParticleType(((IOriginalGameplayProcess)Engine).ParticleTypes.SwapBlock.P_Move)
            {
                Color = pressedColor,
                Color2 = pressedColor
            };
        }

        public CassetteSwapBlock(EntityData data, Vector2 offset, MapEntityId id, ISourceSolidWorldFactory worlds)
            : this(data.Position + offset, id, data.Width, data.Height, data.Nodes[0] + offset, data.Int("index"),
                data.Float("tempo", 1f), data.Bool("oldConnectionBehavior", true), data.Bool("noReturn", false),
                HexColorNullable(data, "customColor"), data.Attr("spritePath", ""), data.Float("sideAlpha", 1f),
                data.Bool("held"), worlds.Create(data.Position + offset, false))
        {
        }

        public override void Awake(object scene)
        {
            Image cross =
                new(GameAtlas[SuffixFromSpritePathOrDefault("/x", "objects/CommunalHelper/cassetteMoveBlock/x")]);
            Image crossPressed =
                new(GameAtlas[
                    SuffixFromSpritePathOrDefault("/xPressed", "objects/CommunalHelper/cassetteMoveBlock/xPressed")]);

            base.Awake(scene);
            if (noReturn)
            {
                AddCenterSymbol(cross, crossPressed);
            }

            ((ISourceScene)scene).SourceScene.QueueSourceEntity(new PathRenderer(this));
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(moveSfx);
            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(returnSfx);
        }

        public override void SceneEnd(object scene)
        {
            base.SceneEnd(scene);
            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(moveSfx);
            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(returnSfx);
        }

        private void OnDash(Vector2 direction)
        {
            if (noReturn)
            {
                Swapping = true;
                target = 1 - target;
                float relativeLerp = target == 1 ? lerp : 1 - lerp;
                speed = relativeLerp >= 0.2f
                    ? maxForwardSpeed
                    : ((ISourceScalarEnvironment)Engine).ScalarArithmetic.Lerp(maxForwardSpeed * 0.333f,
                        maxForwardSpeed,
                        relativeLerp / 0.2f);

                ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(moveSfx);
                moveSfx = ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                    "event:/game/05_mirror_temple/swapblock_move", Center);
            }
            else
            {
                Swapping = lerp < 1f;
                target = 1;
                returnTimer = ReturnTime;
                speed = lerp >= 0.2f
                    ? maxForwardSpeed
                    : ((ISourceScalarEnvironment)Engine).ScalarArithmetic.Lerp(maxForwardSpeed * 0.333f,
                        maxForwardSpeed,
                        lerp / 0.2f);

                ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(returnSfx);
                ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(moveSfx);
                if (!Swapping)
                {
                    ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                        "event:/game/05_mirror_temple/swapblock_move_end", Center);
                }
                else
                {
                    moveSfx = ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                        "event:/game/05_mirror_temple/swapblock_move", Center);
                }
            }
        }

        public override void Update()
        {
            base.Update();
            if (noReturn)
            {
                #region noReturn

                float initialLerp = SwapBlockMotion.AdvanceSpeedAndProgress(new ProgressState(this), true);
                if (lerp is 0 or 1)
                    ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(moveSfx);
                if (lerp != initialLerp)
                {
                    Vector2 liftSpeed = (end - start) * speed;
                    Vector2 position = Position;
                    if (target == 1)
                    {
                        liftSpeed = (end - start) * maxForwardSpeed;
                    }

                    if (lerp < initialLerp)
                    {
                        liftSpeed *= -1f;
                    }

                    if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.02f, Engine))
                    {
                        // Allows move particles in both directions
                        MoveParticles((end - start) * (target - 0.5f) * 2);
                    }

                    Vector2 to = Vector2Interpolation.Lerp(start, end, lerp,
                        ((ISourceScalarEnvironment)Engine).ScalarArithmetic);
                    Vector2 diff = to - (ExactPosition - BlockOffset);
                    MoveH(diff.X, liftSpeed.X);
                    MoveV(diff.Y, liftSpeed.Y);

                    if (position != Position)
                    {
                        ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Position(moveSfx, Center);
                        if (Position - BlockOffset == start || Position == end)
                        {
                            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(moveSfx);
                            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                                "event:/game/05_mirror_temple/swapblock_move_end", Center);
                        }
                    }
                }

                if (Swapping && lerp >= 1f)
                    Swapping = false;

                StopPlayerRunIntoAnimation = lerp is <= 0f or >= 1f;

                #endregion
            }
            else
            {
                #region return

                if (returnTimer > 0f)
                {
                    returnTimer -= Engine.DeltaTime;
                    if (returnTimer <= 0f)
                    {
                        target = 0;
                        speed = 0f;
                        returnSfx = ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                            "event:/game/05_mirror_temple/swapblock_return", Center);
                    }
                }

                float initialLerp = SwapBlockMotion.AdvanceSpeedAndProgress(new ProgressState(this), false);
                if (lerp == 1)
                    ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(moveSfx);
                if (lerp != initialLerp)
                {
                    Vector2 liftSpeed = (end - start) * speed;
                    Vector2 position = Position;
                    if (target == 1)
                    {
                        liftSpeed = (end - start) * maxForwardSpeed;
                    }

                    if (lerp < initialLerp)
                    {
                        liftSpeed *= -1f;
                    }

                    if (target == 1 && ((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.02f, Engine))
                    {
                        MoveParticles(end - start);
                    }

                    Vector2 to = Vector2Interpolation.Lerp(start, end, lerp,
                        ((ISourceScalarEnvironment)Engine).ScalarArithmetic);
                    Vector2 diff = to - (ExactPosition - BlockOffset);
                    MoveH(diff.X, liftSpeed.X);
                    MoveV(diff.Y, liftSpeed.Y);

                    if (position != Position)
                    {
                        ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Position(moveSfx, Center);
                        ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Position(returnSfx, Center);
                        if (Position - BlockOffset == start && target == 0)
                        {
                            ((IOriginalGameplayProcess)Engine).AudioSession.SetParameter(returnSfx, "end", 1f);
                            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                                "event:/game/05_mirror_temple/swapblock_return_end", Center);
                        }
                        else if (Position - BlockOffset == end && target == 1)
                        {
                            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play(
                                "event:/game/05_mirror_temple/swapblock_move_end", Center);
                            ((ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Stop(moveSfx);
                        }
                    }
                }

                if (Swapping && lerp >= 1f)
                    Swapping = false;

                StopPlayerRunIntoAnimation = lerp is <= 0f or >= 1f;

                #endregion
            }
        }

        private void MoveParticles(Vector2 normal)
        {
            int num2 = SwapBlockMotion.GetMovementParticleGeometry(this, Engine, normal, ref particlesRemainder,
                out Vector2 position, out Vector2 positionRange, out float direction);
            ((OriginalLevelSceneRoot)Scene).Particles.Emit(Collidable ? moveParticle : moveParticlePressed, num2,
                position,
                positionRange, direction);
        }
    }
}
