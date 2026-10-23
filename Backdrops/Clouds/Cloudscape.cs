using System;
using Celeste.Game.Execution.Graphics;
using System.Numerics;
using System.Linq;
using System.Collections.Generic;
using Celeste.Core.Scheduling;
using Celeste.Core.Randomness;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Settings.Lightning;
using Celeste.Core.Rendering;
using Celeste.Core.Rendering.States;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Binary.Source;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Graphics.Display.Colors;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.Rendering;
using Celeste.Simulation.Presentation.Backdrops.Runtime;

namespace Celeste.Mods.CommunalHelper.Backdrops.Clouds
{
    [CustomBackdrop("CommunalHelper/Cloudscape")]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Backdrops.Cloudscape")]
    public partial class Cloudscape : BackdropPresentation
    {
        private const uint LEVEL_OF_DETAIL = 16;
        private readonly ISourceEngineContext Engine;
        private readonly CloudscapeResources resources;
        private ISourceRandom Random => ((ISourceRandomEnvironment)Engine).Random;

        private float Lerp(float a, float b, float t) =>
            ((ISourceScalarEnvironment)Engine).ScalarArithmetic.Lerp(a, b, t);

        private readonly Func<float, float> bounceIn, cubeIn, expoIn;

        private PackedColor sky;

        private readonly Vector2 offset, parallax;

        private bool lightning;
        private float lightningMinDelay, lightningMaxDelay;
        private float lightningMinDuration, lightningMaxDuration;
        private float lightningIntensity;
        private PackedColor[] lightningColors;
        private PackedColor lightningFlashColor;

        private readonly float innerRotation, outerRotation;
        private readonly float rotationExponent;

        private readonly SourceBlendState blend;

        private SourceTexture2DResource colorBuffer;
        private readonly CloudscapeMesh mesh;
        private readonly WarpedCloud[] clouds;
        private readonly Ring[] rings;

        private readonly PackedColor[] colors;
        private readonly float BufferAlpha;

        private readonly ZoomBehaviors ZoomBehavior;

        private float age;

        public Cloudscape(ISourceEngineContext engine, BinaryPackerElement child)
            : this(engine, new Options(child))
        {
        }

        public Cloudscape(ISourceEngineContext engine, Options options)
            : base()
        {
            Engine = engine;
            var easing = ((ISourceNamedEasingArithmetic)((ISourceEasingEnvironment)engine).EasingArithmetic)
                .NamedEasers;
            bounceIn = easing["BounceIn"];
            cubeIn = easing["CubeIn"];
            expoIn = easing["ExpoIn"];
            resources = CloudscapeResources.For(engine);
            resources.Load();
            UseSpritebatch = false;

            sky = options.HasBackgroundColor
                ? options.Sky
                : PackedColor.Transparent;

            blend = options.Additive
                ? SourceBlendState.Additive
                : SourceBlendState.AlphaBlend;

            offset = options.Offset;
            parallax = options.Parallax;

            lightning = options.Lightning;
            lightningMinDelay = options.LightningMinDelay;
            lightningMaxDelay = options.LightningMaxDelay;
            lightningMinDuration = options.LightningMinDuration;
            lightningMaxDuration = options.LightningMaxDuration;
            lightningIntensity = options.LightningIntensity;
            lightningColors = options.LightningColors;
            lightningFlashColor = options.LightningFlashColor;

            innerRotation = options.InnerRotation;
            outerRotation = options.OuterRotation;
            rotationExponent = options.RotationExponent;

            BufferAlpha = options.BufferAlpha;
            ZoomBehavior = options.ZoomBehavior;

            ((ISourceRandomProcessEnvironment)Engine).RandomProcess.PushRandom(options.Seed);

            mesh = new();

            List<WarpedCloud> clouds = new();
            List<Ring> rings = new();

            int count = options.Count;

            float a = Math.Min(options.InnerRadius, options.OuterRadius);
            float b = Math.Max(options.InnerRadius, options.OuterRadius);
            float d = b - a;

            short id = 0; // cloud ID for color lookup

            // ring iteration
            for (short r = 0; r < count; r++)
            {
                float percent = (float)r / count;

                PackedColor color = ColorArrayLerp(percent * (options.Colors.Length - 1), options.Colors);
                float radius = a + (d * percent);
                float density = Lerp(options.InnerDensity, options.OuterDensity, percent);

                if (density == 0)
                    continue;

                List<WarpedCloud> cloudsInRing = new();

                float rotation = SourceRandomMath.NextFloat(Random, ((float)Math.PI * 2f));

                // cloud iteration
                float angle = 0f;
                while (angle < ((float)Math.PI * 2f))
                {
                    WarpedCloud cloud = new(this, color);
                    clouds.Add(cloud);
                    cloudsInRing.Add(cloud);

                    int index = mesh.VertexCount;

                    AtlasEntry texture = Random.Choose(resources.CloudTextures);
                    float halfHeight = texture.FrameHeight / 2f;

                    float centralAngle = texture.FrameWidth / radius;
                    float step = centralAngle / LEVEL_OF_DETAIL;

                    for (int i = 0; i < LEVEL_OF_DETAIL; i++)
                    {
                        float th = rotation + angle + (step * i);

                        // custom vertices hold polar coordinates. cartesian coordinates are computed in the shader.
                        float uvx = Lerp(texture.LeftUV, texture.RightUV, (float)i / (LEVEL_OF_DETAIL - 1));
                        CloudscapeVertex closer = new(th, radius - halfHeight, new(uvx, texture.TopUV), id, r);
                        CloudscapeVertex farther = new(th, radius + halfHeight, new(uvx, texture.BottomUV), id, r);
                        mesh.AddVertices(closer, farther);

                        if (i != LEVEL_OF_DETAIL - 1)
                        {
                            int o = index + (i * 2);
                            mesh.AddTriangle(o + 0, o + 1, o + 2);
                            mesh.AddTriangle(o + 1, o + 2, o + 3);
                        }
                    }

                    ++id;
                    angle += centralAngle / density;
                }

                // add ring to regroup clouds
                rings.Add(new(percent, cloudsInRing.ToArray()));
            }

            mesh.Bake();

            this.clouds = clouds.ToArray();
            this.rings = rings.ToArray();

            EnsureValidColorBuffer();
            colors = new PackedColor[this.clouds.Length];

            ((ISourceRandomProcessEnvironment)Engine).RandomProcess.PopRandom();
        }

        private void EnsureValidColorBuffer()
        {
            if (colorBuffer is null || colorBuffer.IsDisposed)
                colorBuffer = new(((ISourceGraphicsDeviceEnvironment)Engine).GraphicsDevice, clouds.Length, 1);
        }

        private VirtualRenderTarget EnsureValidBuffer()
        {
            var gameplay = ((OriginalGameGraphicsResources)((ISourceGameGraphicsEnvironment)Engine).GameGraphics)
                .Buffers.Gameplay;
            return resources.GetBuffer(ZoomBehavior == ZoomBehaviors.StaySame, gameplay);
        }

        public void ConfigureColors(PackedColor bg, PackedColor[] gradientFrom, PackedColor[] gradientTo, float lerp)
        {
            sky = bg;
            foreach (Ring ring in rings)
            {
                PackedColor from = ColorArrayLerp(ring.Lerp * (gradientFrom.Length - 1), gradientFrom);
                PackedColor to = ColorArrayLerp(ring.Lerp * (gradientTo.Length - 1), gradientTo);
                ring.ApplyIdleColor(PackedColor.Lerp(from, to, lerp), colors);
            }
        }

        public void ConfigureLightning(
            bool enable,
            PackedColor[] lightningColors, PackedColor lightningFlashColor,
            float lightningMinDelay, float lightningMaxDelay,
            float lightningMinDuration, float lightningMaxDuration,
            float lightningIntensity)
        {
            lightning = enable;
            if (!lightning)
                return;

            this.lightningColors = lightningColors;
            this.lightningFlashColor = lightningFlashColor;
            this.lightningMinDelay = lightningMinDelay;
            this.lightningMaxDelay = lightningMaxDelay;
            this.lightningMinDuration = lightningMinDuration;
            this.lightningMaxDuration = lightningMaxDuration;
            this.lightningIntensity = lightningIntensity;
        }

        public override void Update(ISourceBackdropScene scene)
        {
            base.Update(scene);

            if (!Visible)
                return;

            age += Engine.DeltaTime;

            // calculate colors once for each cloud, and store them in the color buffer texture.
            // it will be sent to the gpu so it can be sampled, instead of changing the color of each vertex (old & slow method)
            for (int i = 0; i < clouds.Length; i++)
            {
                var cloud = clouds[i];
                cloud.Update(lightning);
                colors[i] = cloud.CalculateColor();
            }
        }

        public override void BeforeRender(ISourceBackdropScene scene)
        {
            base.BeforeRender(scene);

            EnsureValidColorBuffer();
            colorBuffer.SetData(colors);
        }

        public override void Render(ISourceRenderContext context, ISourceBackdropScene scene)
        {
            if (scene is not OriginalLevelSceneRoot level) return;
            var camera = level.Camera;
            float zoom = camera.Viewport.Width != 320 ? level.Zoom : 1f;
            var cameraZoomOutOffset = new Vector2(160f / zoom - 160f, 90f / zoom - 90f);
            var translate = offset - (camera.Position + cameraZoomOutOffset) * parallax;
            var device = ((ISourceGraphicsDeviceEnvironment)Engine).GraphicsDevice;
            SourceRenderTarget2DResource target = level.Buffers.Level.Target;
            var current = device.GetRenderTargets();
            if (current.Length > 0) target = current[0].RenderTarget as SourceRenderTarget2DResource ?? target;
            var buffer = EnsureValidBuffer();
            device.SetRenderTarget(buffer.Target);
            device.Clear(sky);
            device.BlendState = SourceBlendState.AlphaBlend;
            device.RasterizerState = SourceRasterizerState.CullNone;
            var effect = resources.Effect;
            var parameters = effect.Parameters;
            parameters["atlas_texture"].SetValue(resources.Atlas.Sources[0].TextureSafe);
            parameters["color_texture"].SetValue(colorBuffer);
            parameters["ring_count"].SetValue(rings.Length);
            parameters["color_buffer_size"].SetValue(colorBuffer.Width);
            parameters["offset"]
                .SetValue(ZoomBehavior == ZoomBehaviors.Adjust ? translate + cameraZoomOutOffset : translate);
            parameters["inner_rotation"].SetValue(innerRotation);
            parameters["outer_rotation"].SetValue(outerRotation);
            parameters["rotation_exponent"].SetValue(rotationExponent);
            parameters["time"].SetValue(age);
            parameters["dimensions"].SetValue(new Vector2(buffer.Width, buffer.Height));
            foreach (var pass in effect.Techniques[0].Passes)
            {
                pass.Apply();
                mesh.Draw(device);
            }

            device.SamplerStates[0] = SourceSamplerState.LinearWrap;
            device.SamplerStates[1] = SourceSamplerState.LinearWrap;
            device.SetRenderTarget(target);
            Renderer.StartSpritebatch(scene, blend);
            var batch = ((ISourceDrawScope)Engine).Draw.SpriteBatch;
            if (ZoomBehavior == ZoomBehaviors.StaySame)
                batch.Draw(buffer.Target, Vector2.Zero, null, (PackedColor.White * BufferAlpha).PackedValue, 0f,
                    Vector2.Zero, 1f / zoom, SourceSpriteEffects.None, 0f);
            else if (ZoomBehavior == ZoomBehaviors.Adjust)
                batch.Draw(buffer.Target, Vector2.Zero, (PackedColor.White * BufferAlpha).PackedValue);
            Renderer.EndSpritebatch(scene);
        }

        public override void Ended(ISourceBackdropScene scene)
        {
            base.Ended(scene);
            colorBuffer.Dispose();
            colorBuffer = null;
        }
    }
}
