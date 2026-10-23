using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Everest.Gameplay.Platforms.Dream;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Game.Entities.Platforms.Dream;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    [EverestTrackedAs(typeof(OriginalDreamBlockState), true)]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CustomDreamBlock")]
    public abstract partial class CustomDreamBlock : EverestDreamBlockState
    {
        protected readonly ISourceEngineContext Engine;
        internal new ISourceEngineContext EngineContext => Engine;
        protected ISourceRandom RandomSource => ((ISourceRandomEnvironment)Engine).Random;
        protected ISourceVectorArithmetic Vectors => ((ISourceVectorEnvironment)Engine).VectorArithmetic;
        public new bool PlayerHasDreamDash => SourcePlayerHasDreamDash;
        public bool FeatherMode;
        public float DashSpeed;
        protected int RefillCount;
        protected bool shattering;
        public float ColorLerp;
        public bool QuickDestroy;
        protected bool leftWobble = true, rightWobble = true, topWobble = true, bottomWobble = true;
        protected bool awake;
        private bool delayedSetupParticles;
        private bool shakeToggle;
        private readonly ParticleTypePresentation shakeParticle;
        private readonly float[] particleRemainders = new float[4];
        protected AtlasEntry[] featherTextures, doubleRefillStarTextures;
        protected new DreamParticle[] particles;
        public EntityData creatingData;
        public PackedColor ActiveLineColor => SourceActiveLineColor;
        public PackedColor DisabledLineColor => SourceDisabledLineColor;
        public PackedColor ActiveBackColor => SourceActiveBackColor;
        public PackedColor DisabledBackColor => SourceDisabledBackColor;

        public static readonly PackedColor[] DreamColors =
        {
            PackedColor.FromHex("FFEF11"), PackedColor.FromHex("FF00D0"), PackedColor.FromHex("08a310"),
            PackedColor.FromHex("5fcde4"), PackedColor.FromHex("7fb25e"), PackedColor.FromHex("E0564C"),
            PackedColor.FromHex("5b6ee1"), PackedColor.FromHex("CC3B3B"), PackedColor.FromHex("7daa64")
        };

        protected CustomDreamBlock(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : this(engine, data.Position + offset, data.Width, data.Height, data.Bool("featherMode"),
                data.Float("dashSpeed", 240f),
                data.Bool("oneUse"), GetRefillCount(data), data.Bool("below"), data.Bool("quickDestroy"), worlds)
        {
            creatingData = data;
        }

        protected CustomDreamBlock(ISourceEngineContext engine, Vector2 position, int width, int height,
            bool featherMode,
            float dashSpeed, bool oneUse, int refillCount, bool below, bool quickDestroy,
            ISourceSolidWorldFactory worlds)
            : base(engine, position, width, height, null, false, oneUse, below, worlds)
        {
            Engine = engine;
            QuickDestroy = quickDestroy;
            RefillCount = refillCount;
            FeatherMode = featherMode;
            DashSpeed = dashSpeed;
            shakeParticle =
                new ParticleTypePresentation(((IOriginalGameplayProcess)engine).ParticleTypes.SwitchGate.P_Behind)
                {
                    Color = ActiveLineColor, ColorMode = ParticleTypePresentation.ColorModes.Static,
                    Acceleration = Vector2.Zero, DirectionRange = (float)Math.PI / 2
                };
            var atlas = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;
            featherTextures = new[]
            {
                atlas["particles/CommunalHelper/featherBig"], atlas["particles/CommunalHelper/featherMedium"],
                atlas["particles/CommunalHelper/featherSmall"]
            };
            var star = atlas["objects/CommunalHelper/customDreamBlock/particles"];
            doubleRefillStarTextures = new[]
            {
                star.GetSubtexture(14, 0, 7, 7), star.GetSubtexture(7, 0, 7, 7), star.GetSubtexture(0, 0, 7, 7),
                star.GetSubtexture(7, 0, 7, 7)
            };
        }

        protected static int GetRefillCount(EntityData data) =>
            data.Bool("doubleRefill") ? 2 : data.Int("refillCount", -1);

        public override void Setup() => SetupCustomParticles(Width, Height);

        public override void Awake(object scene)
        {
            base.Awake(scene);
            awake = true;
            if (delayedSetupParticles) SetupCustomParticles(Width, Height);
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            Engine.Effects.Glitch = 0f;
        }

        public override void Update()
        {
            base.Update();
            if (FeatherMode) UpdateParticles();
            if (Visible && PlayerHasDreamDash && SourceOneUse &&
                ((ISourceScene)Scene).SourceScene.Clock.OnInterval(.03f, Engine))
            {
                var shake = SourceShake;
                if (shakeToggle) shake.X = RandomSource.Next(-1, 2);
                else shake.Y = RandomSource.Next(-1, 2);
                SourceShake = shake;
                shakeToggle = !shakeToggle;
                if (!shattering) ShakeParticles();
            }
        }

        protected override PackedColor RenderActiveBackColor => Lerp(ActiveBackColor, PackedColor.White, ColorLerp);
        protected override PackedColor RenderDisabledBackColor => Lerp(DisabledBackColor, PackedColor.White, ColorLerp);
        protected override bool RenderTopWobble => topWobble;
        protected override bool RenderRightWobble => rightWobble;
        protected override bool RenderBottomWobble => bottomWobble;
        protected override bool RenderLeftWobble => leftWobble;

        protected override DreamParticleVisual GetParticleVisual(int index, Vector2 cameraPosition)
        {
            DreamParticleVisual visual = base.GetParticleVisual(index, cameraPosition);
            visual.Color = Lerp(visual.Color, PackedColor.Black, ColorLerp);
            visual.DrawOffset = Vector2.Zero;
            if (FeatherMode)
            {
                var particle = particles[index];
                float rotation = (float)Math.PI / 2f - .8f +
                                 (float)Math.Sin(particle.RotationCounter * particle.MaxRotate);
                visual.Position += Vector2Geometry.AngleToVector(rotation, 4f);
                visual.Texture = featherTextures[particle.Layer];
                visual.Rotation = rotation;
            }
            else if (RefillCount != -1) visual.Texture = GetDreamParticleTexture(index, doubleRefillStarTextures);

            return visual;
        }

        protected bool CheckParticleCollide(Vector2 p) =>
            p.X >= X + 2f && p.Y >= Y + 2f && p.X < Right - 2f && p.Y < Bottom - 2f;

        protected void WobbleLine(ISourceRenderContext context, Vector2 from, Vector2 to, float offset) =>
            RenderDreamWobbleLine(context, from, to, offset);

        protected Vector2 PutInside(Vector2 p) => WrapDreamParticlePosition(p);

        protected PackedColor Lerp(PackedColor a, PackedColor b, float amount) =>
            SourceColorInterpolation.Lerp(a, b, amount, ((ISourceScalarEnvironment)Engine).ScalarArithmetic);

        protected ref OriginalDreamBlockState.DreamParticle PhysicalParticle(int i) => ref base.particles[i];

        protected void AllocateParticles(int count)
        {
            particles = new DreamParticle[count];
            base.particles = new OriginalDreamBlockState.DreamParticle[count];
        }

        protected new struct DreamParticle
        {
            private readonly CustomDreamBlock owner;
            private readonly int index;

            public Vector2 Position
            {
                get => owner.PhysicalParticle(index).Position;
                set => owner.PhysicalParticle(index).Position = value;
            }

            public int Layer
            {
                get => owner.PhysicalParticle(index).Layer;
                set => owner.PhysicalParticle(index).Layer = value;
            }

            public PackedColor Color
            {
                get => owner.PhysicalParticle(index).Color;
                set => owner.PhysicalParticle(index).Color = value;
            }

            public float TimeOffset
            {
                get => owner.PhysicalParticle(index).TimeOffset;
                set => owner.PhysicalParticle(index).TimeOffset = value;
            }

            public float Speed, Spin, MaxRotate, RotationCounter;

            public DreamParticle(CustomDreamBlock owner, int index) : this()
            {
                this.owner = owner;
                this.index = index;
            }
        }
    }
}
