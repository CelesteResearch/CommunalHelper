// Copyright (c) 2020 Flynn Swainston-Calcutt. Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Audio;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Directions = Celeste.Simulation.Gameplay.Platforms.MoveBlock.MoveBlockDirection;
using Color = Celeste.Core.Display.PackedColor;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;

namespace Celeste.Mods.CommunalHelper.Entities.MoveBlocks.Redirecting
{
    [CustomEntity("CommunalHelper/MoveBlockRedirect")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.MoveBlockRedirect")]
    public partial class MoveBlockRedirect : SceneEntityState
    {
        public enum Operations
        {
            Add,
            Subtract,
            Multiply
        }

        public Operations Operation;
        public float Modifier;

        private ParticleType p_Used;

        public static readonly Color Mask = new(200, 180, 190);
        public static readonly Color UsedColor = PackedColor.FromHex("474070"); // From MoveBlock
        public static readonly Color DeleteColor = PackedColor.FromHex("cc2541");
        public static readonly Color DefaultColor = PackedColor.FromHex("fbce36");
        public static readonly Color FasterColor = PackedColor.FromHex("29c32f");
        public static readonly Color SlowerColor = PackedColor.FromHex("1c5bb3");

        private Color startColor;
        private Color? overrideColor, overrideUsedColor;

        public Directions Direction;
        public bool FastRedirect;
        public bool OneUse, DeleteBlock;

        private readonly float angle;
        private float maskAlpha, alpha = 1f;
        private List<Image> borders;

        private Redirectable lastMoveBlock;

        private readonly string reskinFolder;
        private MoveBlockRedirectIcon icon;
        private readonly ISourceEngineContext engine;
        private readonly Func<float, float> bounceIn;
        private readonly string redirectableKey;
        private AtlasData Atlas => ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;

        public MoveBlockRedirect(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            bounceIn =
                ((ISourceNamedEasingArithmetic)((ISourceEasingEnvironment)engine).EasingArithmetic).NamedEasers[
                    "BounceIn"];
            redirectableKey = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog.Find(typeof(Redirectable))
                .RegistryKey;
            Depth = -9001;
            Collider = new Hitbox(data.Width, data.Height);

            FastRedirect = data.Bool("fastRedirect");
            OneUse = data.Bool("oneUse");
            DeleteBlock = data.Bool("deleteBlock") || (Operation == Operations.Multiply && Modifier == 0f);

            Operation = data.Enum("operation", Operations.Add);
            Modifier = Math.Abs(data.Float("modifier"));

            overrideColor = HexColorNullable(data, "overrideColor");
            overrideUsedColor = HexColorNullable(data, "overrideUsedColor");

            if (float.TryParse(data.Attr("direction"), out float fAngle))
                angle = fAngle;
            else
            {
                Direction = data.Enum("direction", Directions.Left);
                angle = DirectionAngle(Direction);
            }

            string str = data.Attr("reskinFolder").Trim().TrimEnd('/');
            reskinFolder = str == "" ? null : "objects/" + str;

            AddTextures();
        }

        private void AddTextures()
        {
            borders = new List<Image>();

            MTexture defaultTexture = Atlas["objects/CommunalHelper/moveBlockRedirect/block"];
            MTexture block;
            if (reskinFolder is null)
            {
                block = defaultTexture;
            }
            else
            {
                Atlas.PushFallback(defaultTexture);
                block = Atlas[reskinFolder + "/block"];
                Atlas.PopFallback();
            }

            int w = (int)(Width / 8f);
            int h = (int)(Height / 8f);
            for (int i = -1; i <= w; i++)
            {
                for (int j = -1; j <= h; j++)
                {
                    int tx = (i == -1) ? 0 : ((i == w) ? 16 : 8);
                    int ty = (j == -1) ? 0 : ((j == h) ? 16 : 8);
                    AddImage(block.GetSubtexture(tx, ty, 8, 8), new Vector2(i, j) * 8, borders);
                }
            }

            // Unused in favor of large arrow
            /*
        int x = 8;
        for (int y = 8; y <= Height; y += 8) {
            for (; x <= Width; x += 16) {
                Image image = new Image(Atlas["objects/CommunalHelper/moveBlockRedirect/arrow"]);
                image.Position = new Vector2(x, y);
                image.Color = new Color(100, 80, 120) * 0.5f;
                image.Rotation = angle;
                arrows.OwnedComponents.Add(image);
                OwnedComponents.Add(image);
            }
            x = ((y / 8) % 2 == 0) ? 8 : 16;
        }
        */
        }

        private void AddImage(MTexture texture, Vector2 position, List<Image> addTo)
        {
            Image image = new(texture)
            {
                Position = position
            };
            OwnedComponents.Add(image);
            addTo?.Add(image);
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            string iconTexture = "arrow";
            startColor = DefaultColor;

            if (DeleteBlock)
            {
                iconTexture = "x";
                startColor = DeleteColor;
            }
            else
            {
                if ((Operation == Operations.Add && Modifier != 0f) ||
                    (Operation == Operations.Multiply && Modifier > 1f))
                {
                    iconTexture = "fast";
                    startColor = FasterColor;
                }
                else if ((Operation == Operations.Subtract && Modifier != 0f) ||
                         (Operation == Operations.Multiply && Modifier < 1f))
                {
                    iconTexture = "slow";
                    startColor = SlowerColor;
                }
            }

            ((ISourceScene)scene).SourceScene.QueueSourceEntity(icon =
                new MoveBlockRedirectIcon(engine, Center, angle, iconTexture, reskinFolder));
            UpdateAppearance();
            p_Used = new ParticleType(((IOriginalGameplayProcess)engine).ParticleTypes.SwitchGate.P_Behind)
            {
                Color = icon.Sprite.Color,
                Color2 = Color.Lerp(icon.Sprite.Color, Color.White, .2f),
                DirectionRange = (float)Math.PI * 0.5f,
                SpeedMax = 20f
            };
        }

        private void UpdateAppearance()
        {
            Color currentColor = Color.Lerp(overrideColor ?? startColor, overrideUsedColor ?? UsedColor, maskAlpha) *
                                 alpha;
            icon.Sprite.Color = currentColor;
            foreach (Image image in borders)
            {
                image.Color = currentColor * alpha;
            }
        }

        public override void Update()
        {
            base.Update();
            UpdateAppearance();

            if (lastMoveBlock is not null && !SourceEntityCollision.Check(this, (SceneEntityState)lastMoveBlock.Entity))
                lastMoveBlock = null;
            else if ((lastMoveBlock is null || FastRedirect || !lastMoveBlock.IsRedirectable) && maskAlpha != 0f)
            {
                maskAlpha = ScalarMath.Approach(maskAlpha, 0f,
                    (FastRedirect && !DeleteBlock ? 2.5f : 4f) * engine.DeltaTime,
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
            }

            Redirectable redirectable =
                ((ISourceScene)Scene).SourceScene.Components.GetTrackedComponents(redirectableKey)
                .FirstOrDefault(c => SourceEntityCollision.Check(this, (SceneEntityState)c.Entity)) as Redirectable;
            SceneEntityState block = (SceneEntityState)redirectable?.Entity; // Non-null if redirectible isn't null

            if (Collidable && redirectable is not null && redirectable != lastMoveBlock &&
                redirectable.IsRedirectable &&
                block.Width == Width && block.Height == Height)
            {
                if (!Contains(Collider, block.Collider, 0.001f))
                {
                    Directions dir = redirectable.Direction;
                    Vector2 prevPosOffset = -DirectionVector(dir, redirectable.Speed);

                    float edgeMin;
                    float edgeMax;
                    bool wentThrough = false;
                    if (dir is Directions.Down or Directions.Up)
                    {
                        edgeMin = Math.Min(block.Top, block.Top + prevPosOffset.Y);
                        edgeMax = Math.Max(block.Bottom, block.Bottom + prevPosOffset.Y);
                        wentThrough = X == block.X && edgeMin <= Top && edgeMax >= Bottom;
                    }
                    else
                    {
                        edgeMin = Math.Min(block.Left, block.Left + prevPosOffset.X);
                        edgeMax = Math.Max(block.Right, block.Right + prevPosOffset.X);
                        wentThrough = Y == block.Y && edgeMin <= Left && edgeMax >= Right;
                    }

                    if (!wentThrough)
                        return;
                }

                lastMoveBlock = redirectable;

                if (DeleteBlock)
                {
                    SimulationCoroutine routine = block.OfType<SimulationCoroutine>().FirstOrDefault();
                    block.OwnedComponents.Remove(routine);
                    block.OwnedComponents.Add(new SimulationCoroutine(engine,
                        BreakBlock(redirectable, routine, FastRedirect, OneUse)));
                }
                else
                {
                    if (FastRedirect)
                    {
                        SetBlockData(redirectable);
                        maskAlpha = 1f;
                        if (OneUse)
                            Disappear();
                    }
                    else
                    {
                        SimulationCoroutine routine = block.OfType<SimulationCoroutine>().FirstOrDefault();
                        block.OwnedComponents.Remove(routine);
                        OwnedComponents.Add(new SimulationCoroutine(engine, RedirectRoutine(redirectable, routine)));
                    }
                }
            }
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            icon.RemoveSelf();
        }

        private void SetBlockData(Redirectable redirectable)
        {
            redirectable.Angle = angle;
            redirectable.Direction = Direction;

            float newSpeed = ApplyTo(Operation, redirectable.TargetSpeed, Modifier);

            redirectable.TargetSpeed = Math.Max(10f, newSpeed); // could go into negative speeds, yuck
            lastMoveBlock = redirectable;
            redirectable.MoveTo(Position);

            UsedParticles();
        }

        private IEnumerator BreakBlock(Redirectable redirectable, SimulationCoroutine orig, bool fast, bool oneUse)
        {
            redirectable.MoveTo(Position);
            redirectable.BeforeBreakAnimation();

            //state = MovementState.Breaking;
            redirectable.ResetBlock();
            redirectable.Speed = redirectable.TargetSpeed = 0f;

            //redirectable.Entity.StopPlayerRunIntoAnimation = true; // Unused in Vanilla so we ignore it

            if (fast)
            {
                maskAlpha = 1f;
                ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                    "event:/CommunalHelperEvents/game/redirectMoveBlock/arrowblock_break",
                    ((SceneEntityState)redirectable.Entity).Position);
            }
            else
            {
                float duration = 0.15f;
                float timer = 0f;
                yield return .2f;
                ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play("event:/game/04_cliffside/arrowblock_break",
                    ((SceneEntityState)redirectable.Entity).Position);
                while (timer < duration)
                {
                    timer += engine.DeltaTime;
                    maskAlpha = ((ISourceEasingEnvironment)engine).EasingArithmetic.SineIn(timer / duration);
                    yield return null;
                }
            }

            UsedParticles();

            // Absolutely cursed beyond belief

            redirectable.OnBreak(orig);
            ((SceneEntityState)redirectable.Entity).OwnedComponents.Add(orig);

            yield return null;

            if (oneUse)
                Disappear();
        }

        private IEnumerator RedirectRoutine(Redirectable redirectable, SimulationCoroutine orig)
        {
            SceneEntityState block = (SceneEntityState)redirectable.Entity;
            float duration = 1f;

            redirectable.MoveTo(Position);

            redirectable.OnPause(orig);

            float timer = 0f;
            while (timer < duration)
            {
                timer += engine.DeltaTime;
                maskAlpha = bounceIn(timer / duration);
                yield return null;
            }

            SetBlockData(redirectable);

            while (timer > 0.2f)
            {
                timer -= engine.DeltaTime;
                float percent = timer / duration;
                maskAlpha = bounceIn(percent);
                yield return null;
            }

            redirectable.BeforeResumeAnimation();

            while (timer > 0)
            {
                timer -= engine.DeltaTime;
                maskAlpha = bounceIn(timer / duration);
                yield return null;
            }

            // Absolutely cursed, starts the Controller routine after a certain number of yields
            redirectable.OnResume(orig);
            ((SceneEntityState)redirectable.Entity).OwnedComponents.Add(orig);

            // Wait for the moveblock to continue before resetting
            if (OneUse)
                Disappear();
        }

        private void Disappear()
        {
            Collidable = false;
            OwnedComponents.Add(new SimulationCoroutine(engine, DisappearRoutine()));
        }

        private IEnumerator DisappearRoutine()
        {
            float timer = 1f;
            while (timer > 0f)
            {
                timer -= engine.DeltaTime * 2f;
                alpha = timer;
                yield return null;
            }

            RemoveSelf();
        }

        private void UsedParticles()
        {
            OriginalLevelSceneRoot level = (OriginalLevelSceneRoot)Scene;

            for (int i = 0; i < Height / 8; i++)
            {
                level.Particles.Emit(p_Used,
                    new Vector2(Left + 1f,
                        SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, Top + 3f, Bottom - 3f)),
                    (float)Math.PI);
                level.Particles.Emit(p_Used,
                    new Vector2(Right - 1f,
                        SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, Top + 3f, Bottom - 3f)), 0f);
            }

            for (int i = 0; i < Width / 8; i++)
            {
                level.Particles.Emit(p_Used,
                    new Vector2(
                        SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, Left + 3f, Right - 3f),
                        Top + 1f), -(float)Math.PI / 2f);
                level.Particles.Emit(p_Used,
                    new Vector2(
                        SourceRandomMath.Range(((ISourceRandomEnvironment)engine).Random, Left + 3f, Right - 3f),
                        Bottom - 1f), (float)Math.PI / 2f);
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            SourcePrimitiveDrawing.Rect(context, this, X - 1, Y - 1, Width + 2, Height + 2, Mask * maskAlpha);
            base.Render(context);
        }


        private static PackedColor? HexColorNullable(EntityData data, string key)
        {
            if (data.Values.TryGetValue(key, out object value))
            {
                string text = value.ToString();
                if (text.Length is 6 or 7) return Celeste.Graphics.Display.Colors.SourceHexColor.HexToColor(text);
            }

            return null;
        }

        private static bool Contains(ColliderGeometry container, ColliderGeometry contained, float padding)
            => container.AbsoluteLeft - padding < contained.AbsoluteLeft &&
               container.AbsoluteTop - padding < contained.AbsoluteTop &&
               container.AbsoluteRight + padding > contained.AbsoluteRight &&
               container.AbsoluteBottom + padding > contained.AbsoluteBottom;

        private static float DirectionAngle(Directions direction) => direction switch
        {
            Directions.Left => (float)Math.PI,
            Directions.Up => -(float)Math.PI / 2f,
            Directions.Down => (float)Math.PI / 2f,
            _ => 0f
        };

        private static Vector2 DirectionVector(Directions direction, float factor) => (direction switch
        {
            Directions.Up => -Vector2.UnitY,
            Directions.Down => Vector2.UnitY,
            Directions.Left => -Vector2.UnitX,
            _ => Vector2.UnitX
        }) * factor;

        private static float ApplyTo(Operations operation, float value, float modifier) => operation switch
        {
            Operations.Add => value + modifier,
            Operations.Subtract => value - modifier,
            Operations.Multiply => value * modifier,
            _ => value
        };
    }
}
