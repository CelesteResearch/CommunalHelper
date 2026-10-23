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
using System.Collections;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Game.Controls.Source;
using Celeste.Game.Entities.Platforms.ZipMovers.Motion;
using Celeste.Game.Presentation.Platforms.ZipMovers;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Presentation.Particles.Definitions;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    [CustomEntity("CommunalHelper/CassetteZipMover")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.CassetteZipMover")]
    public partial class CassetteZipMover : CustomCassetteBlock
    {
        private static Vector2 Perpendicular(Vector2 value) => new(-value.Y, value.X);

        private static Rectangle CameraBounds(OriginalLevelSceneRoot level) => new((int)level.Camera.Left,
            (int)level.Camera.Top, (int)level.Camera.Right - (int)level.Camera.Left,
            (int)level.Camera.Bottom - (int)level.Camera.Top);

        public new static CassetteZipMover Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset,
            OriginalLevelEntityData data)
        {
            return new(data, offset, new MapEntityId(room.Name, data.ID), level.SolidWorldFactory);
        }

        private PathRenderer pathRenderer;

        private float percent = 0f;

        private readonly SoundSourceComponentState sfx;

        /// <summary>
        /// Entity nodes with start Position as the first element
        /// </summary>
        protected readonly Vector2[] nodes;

        private readonly bool permanent;
        private readonly bool waits;
        private readonly bool ticking;
        private readonly bool noReturn;

        private readonly MTexture cog, cogPressed, cogWhite;

        public CassetteZipMover(Vector2 position, MapEntityId id, int width, int height, Vector2[] nodes, int index,
            float tempo, bool oldConnectionBehavior, bool noReturn, bool perm, bool waits, bool ticking,
            Color? overrideColor, string spritePath, float sideAlpha, bool held, ISourceSolidWorld world)
            : base(position, id, width, height, index, tempo, true, oldConnectionBehavior, false, overrideColor,
                spritePath,
                sideAlpha, held, world)
        {
            this.noReturn = noReturn;
            permanent = perm;
            this.waits = waits;
            this.ticking = ticking;

            this.nodes = nodes;

            Add(new SimulationCoroutine(EngineContext, Sequence()));
            sfx = ((ISourceSoundSourceEnvironment)EngineContext).SoundSources.Create();

            sfx.Position =
                ((ISourceVectorEnvironment)EngineContext).VectorArithmetic.Divide(new Vector2(Width, Height), 2f);
            Add(sfx);

            cog = GameAtlas[SuffixFromSpritePathOrDefault("/cog", "objects/CommunalHelper/cassetteZipMover/cog")];
            cogPressed =
                GameAtlas[
                    SuffixFromSpritePathOrDefault("/cogPressed", "objects/CommunalHelper/cassetteZipMover/cogPressed")];
            cogWhite = GameAtlas[
                SuffixFromSpritePathOrDefault("/cogWhite", "objects/CommunalHelper/cassetteZipMover/cogWhite")];
        }

        public CassetteZipMover(EntityData data, Vector2 offset, MapEntityId id, ISourceSolidWorldFactory worlds)
            : this(data.Position + offset, id, data.Width, data.Height, data.NodesWithPosition(offset),
                data.Int("index"),
                data.Float("tempo", 1f), data.Bool("oldConnectionBehavior", true), data.Bool("noReturn", false),
                data.Bool("permanent"), data.Bool("waiting"), data.Bool("ticking"),
                HexColorNullable(data, "customColor"),
                data.Attr("spritePath", ""), data.Float("sideAlpha", 1f), data.Bool("held"),
                worlds.Create(data.Position + offset, false))
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
                AddCenterSymbol(cross, crossPressed);
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
            base.Removed(scene);
        }

        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = Position;
            Position += Shake;
            base.Render(context);
            Position = position;
        }

        private void ScrapeParticlesCheck(Vector2 to)
        {
            ZipMoverDrawing.ScrapeParticles(this, EngineContext, to);
        }
    }
}
