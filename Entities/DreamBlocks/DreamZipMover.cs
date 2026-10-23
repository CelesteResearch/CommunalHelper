// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Entities.Platforms.ZipMovers.Motion;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Platforms.ZipMovers;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;
using System.Runtime.CompilerServices;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Mathematics.Easing;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    [CustomEntity("CommunalHelper/DreamZipMover")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.DreamZipMover")]
    public partial class DreamZipMover : CustomDreamBlock, IMovingDreamBlockEntry
    {
        private const float impactSoundOffset = 0.92f;

        private PathRenderer pathRenderer;

        private readonly SoundSourceComponentState sfx;

        private readonly Vector2[] nodes;
        private readonly bool permanent;
        private readonly bool waits;
        private readonly bool ticking;
        private readonly bool dreamAesthetic;
        private readonly bool noReturn;

        private readonly MTexture cross;
        private MTexture cog;

        private static readonly Color defaultRopeColor = Color.FromHex("663931");
        private static readonly Color defaultRopeLightColor = Color.FromHex("9b6157");

        private sealed class Textures
        {
            public MTexture Normal, Dream, Disabled, White;

            public void Reload(ISourceEngineContext engine)
            {
                var atlas = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;
                Normal = atlas["objects/zipmover/cog"];
                Dream = atlas["objects/CommunalHelper/dreamZipMover/cog"];
                Disabled = atlas["objects/CommunalHelper/dreamZipMover/disabledCog"];
                White = atlas["objects/CommunalHelper/dreamZipMover/cogWhite"];
            }
        }

        private static readonly ConditionalWeakTable<ISourceEngineContext, Textures> textureOwners = new();
        private readonly Textures textures;

        private float percent;

        // Flag used to link zippers of the same color. Null if the zipper is not linked.
        private readonly string linkFlag;

        private readonly Color ropeColor, ropeLightColor;

        public static DreamZipMover Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset, level.SolidWorldFactory);

        public DreamZipMover(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : base(engine, data, offset, worlds)
        {
            textures = textureOwners.GetValue(engine, value =>
            {
                var t = new Textures();
                t.Reload(value);
                return t;
            });
            nodes = new Vector2[data.Nodes.Length + 1];
            nodes[0] = data.Position + offset;
            for (int i = 0; i < data.Nodes.Length; i++) nodes[i + 1] = data.Nodes[i] + offset;

            noReturn = data.Bool("noReturn");
            dreamAesthetic = data.Bool("dreamAesthetic");

            permanent = data.Bool("permanent");
            waits = data.Bool("waiting");
            ticking = data.Bool("ticking");

            string ropeColorCode = data.Attr("ropeColor", "");
            if (!string.IsNullOrWhiteSpace(ropeColorCode))
            {
                ropeColor = Color.FromHex(ropeColorCode);
                ropeLightColor = ropeColor * 1.1f; // matches Adventure Helper
            }
            else
            {
                ropeColor = defaultRopeColor;
                ropeLightColor = defaultRopeLightColor;
                ropeColorCode = "663931";
            }

            if (data.Bool("linked"))
            {
                linkFlag = $"ZipMoverSync:{ropeColorCode}"; // matches Adventure Helper
            }

            OwnedComponents.Add(new SimulationCoroutine(Engine, Sequence()));
            OwnedComponents.Add(new LightOccludeState());
            OwnedComponents.Add(sfx = new SoundSourceComponentState(engine)
            {
                Position = new Vector2(Width / 2f, Height / 2f)
            });
            cross = ((ISourceGameGraphicsEnvironment)Engine).GameGraphics.GameAtlas[
                "objects/CommunalHelper/dreamMoveBlock/x"];

            UpdateCogTexture();
        }

        private void UpdateCogTexture()
        {
            cog = dreamAesthetic ? (PlayerHasDreamDash ? textures.Dream : textures.Disabled) : textures.Normal;
        }

        public override void Added(object scene)
        {
            base.Added(scene);

            ((ISourceScene)scene).SourceScene.QueueSourceEntity(pathRenderer = new(this, nodes));
        }

        public override void Removed(object scene)
        {
            ((ISourceScene)scene).SourceScene.RemoveEntity(pathRenderer);
            pathRenderer = null;
            SignalLinkedZippers(false);
            base.Removed(scene);
        }

        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = Position;
            Position += Shake;

            base.Render(context);

            if (noReturn)
                SourceAtlasDrawing.DrawCentered(context, this, cross, Center + SourceShake, Color.White);

            Position = position;
        }

        private void ScrapeParticlesCheck(Vector2 to) => ZipMoverDrawing.ScrapeParticles(this, Engine, to);

        // Whether the zipper should activate.
        // isMain is whether the zipper should play sounds and do other effects
        private bool ShouldActivate(out bool isMain)
        {
            if (Scene is not OriginalLevelSceneRoot level)
                return isMain = HasPlayerRider();

            // Handle linked zip movers
            if (linkFlag is { } && level.Session.GetFlag(linkFlag))
            {
                isMain = false;
                return true;
            }

            return isMain = HasPlayerRider();
        }

        // Controls the flag related to linked zip movers
        private void SignalLinkedZippers(bool shouldActivate)
        {
            if (linkFlag is { } && Scene is OriginalLevelSceneRoot level)
            {
                level.Session.SetFlag(linkFlag, shouldActivate);
            }
        }

        protected override void OneUseDestroy()
        {
            base.OneUseDestroy();
            ((ISourceScene)Scene).SourceScene.RemoveEntity(pathRenderer);
            pathRenderer = null;
            sfx.Stop();
        }

        public override void Update()
        {
            base.Update();
            UpdateCogTexture();
        }

        public void DrawBorder()
        {
        }

        public void DrawCog(ISourceRenderContext context, Vector2 node, float rotation)
        {
            SourceAtlasDrawing.DrawCentered(context, this, cog, node + Vector2.UnitY, Color.Black, 1f, rotation);
            SourceAtlasDrawing.DrawCentered(context, this, cog, node, Color.White, 1f, rotation);
        }

        internal static void InitializeTextures(ISourceEngineContext engine)
            => textureOwners.GetOrCreateValue(engine).Reload(engine);
    }
}
