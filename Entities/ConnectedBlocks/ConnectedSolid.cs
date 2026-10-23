// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Graphics.Content;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Everest.Gameplay.Visibility;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Collider = Celeste.Physics.Collision.Shapes.ColliderGeometry;
using ColliderList = Celeste.Physics.Collision.Shapes.ColliderListGeometry;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedSolid : SourceSolidEntityState
    {
        public BGTilesRenderer BGRenderer;

        public Vector2 GroupBoundsMin, GroupBoundsMax;
        public Vector2 GroupCenter => Position + GroupOffset + ((GroupBoundsMax - GroupBoundsMin) / 2f);

        public Rectangle GetGroupBoundsAt(Vector2 position)
        {
            Vector2 pos = position + GroupOffset + Shake;
            Vector2 size = GroupBoundsMax - GroupBoundsMin;

            return new Rectangle((int)pos.X, (int)pos.Y, (int)size.X, (int)size.Y);
        }

        // AllColliders contains colliders that didn't have a hitbox.
        public Hitbox[] Colliders, AllColliders;
        public Collider MasterCollider;

        // Auto-tiling stuff. (AllGroupTiles is similar to AllColliders)
        public bool[,] GroupTiles, AllGroupTiles;
        private AutoTileData[,] autoTileData;
        private bool wasAutoTiled = false;

        private enum TileType
        {
            Edge,
            Corner,
            InnerCorner,
            Filler
        }

        private readonly struct AutoTileData(int x, int y, TileType type)
        {
            public readonly int X = x, Y = y;
            public readonly TileType Type = type;
        }

        public Vector2 GroupOffset;

        public int MasterWidth, MasterHeight;
        public Vector2 MasterCenter => MasterCollider.Center + Position;

        public readonly List<Image> Tiles = [];
        public readonly List<Image> EdgeTiles = [];
        public readonly List<Image> CornerTiles = [];
        public readonly List<Image> InnerCornerTiles = [];
        public readonly List<Image> FillerTiles = [];

        public ConnectedSolid(Vector2 position, int width, int height, bool safe, ISourceSolidWorld world)
            : base(position, width, height, safe, world)
        {
            GroupBoundsMin = new Vector2(X, Y);
            GroupBoundsMax = new Vector2(Right, Bottom);
            MasterWidth = width;
            MasterHeight = height;
            MasterCollider = Collider;
        }

        public override void Awake(object scene)
        {
            List<SolidExtension> extensions = [];
            FindExtensions(extensions);

            GroupOffset = new Vector2(GroupBoundsMin.X, GroupBoundsMin.Y) - Position;
            Colliders = new Hitbox[extensions.Count(ext => ext.HasHitbox) + 1];
            AllColliders = new Hitbox[extensions.Count + 1];

            int j = 0;
            for (int i = 0; i < extensions.Count; i++)
            {
                SolidExtension e = extensions[i];
                Vector2 offset = e.Position - Position;
                Hitbox hitbox = new(e.Width, e.Height, offset.X, offset.Y);
                if (e.HasHitbox)
                {
                    Colliders[j] = hitbox;
                    j++;
                }

                AllColliders[i] = hitbox;
                e.RemoveSelf();
                // You don't want disabled Solids hanging around in the level, so you remove them.
            }

            int tWidth = (int)((GroupBoundsMax.X - GroupBoundsMin.X) / 8);
            int tHeight = (int)((GroupBoundsMax.Y - GroupBoundsMin.Y) / 8);
            GroupTiles = new bool[tWidth + 2, tHeight + 2];
            AllGroupTiles = new bool[tWidth + 2, tHeight + 2];

            Colliders[^1] = (Hitbox)Collider;
            AllColliders[^1] = (Hitbox)Collider;

            Collider = new ColliderList(AllColliders);
            for (int x = 0; x < tWidth + 2; x++)
            for (int y = 0; y < tHeight + 2; y++)
                AllGroupTiles[x, y] = TileCollideWithGroup(x - 1, y - 1);

            Collider = new ColliderList(Colliders);
            for (int x = 0; x < tWidth + 2; x++)
            for (int y = 0; y < tHeight + 2; y++)
                GroupTiles[x, y] = TileCollideWithGroup(x - 1, y - 1);

            ((ISourceScene)scene).SourceScene.QueueSourceEntity(BGRenderer = new BGTilesRenderer(this,
                ((ISourceEntitySourceDataEnvironment)SourceWorld.EngineContext).EntitySourceDataRegistration));

            base.Awake(scene);
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            BGRenderer.RemoveSelf();
        }

        public static Tuple<MTexture[,], MTexture[,]> SetupCustomTileset(ISourceEngineContext engine, string path,
            bool withPrefix)
        {
            MTexture tileset =
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[(withPrefix ? "objects/" : "") + path];

            MTexture[,] overrideEdgeTiles = new MTexture[3, 3];
            MTexture[,] overrideInCornersTiles = new MTexture[2, 2];

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    overrideEdgeTiles[i, j] = tileset.GetSubtexture(i * 8, j * 8, 8, 8);
                    if (i < 2 && j < 2)
                        overrideInCornersTiles[i, j] = tileset.GetSubtexture((i * 8) + 24, j * 8, 8, 8);
                }
            }

            return new Tuple<MTexture[,], MTexture[,]>(overrideEdgeTiles, overrideInCornersTiles);
        }

        private void FindExtensions(List<SolidExtension> list)
        {
            foreach (SolidExtension extension in ((ISourceScene)Scene).SourceScene.Entities
                     .GetTrackedEntities(typeof(SolidExtension)).Cast<SolidExtension>())
            {
                if (!extension.HasGroup &&
                    (SourceSceneCollision.CheckRect(new Rectangle((int)X - 1, (int)Y, (int)Width + 2, (int)Height),
                         extension) ||
                     SourceSceneCollision.CheckRect(new Rectangle((int)X, (int)Y - 1, (int)Width, (int)Height + 2),
                         extension)))
                {
                    extension.AddToGroupAndFindChildren(extension, this, list);
                }
            }
        }

        public void SpawnScrapeParticles(bool doOnX = true, bool doOnY = true)
        {
            Collidable = false;

            OriginalLevelSceneRoot level = (OriginalLevelSceneRoot)Scene;
            foreach (Hitbox hitbox in Colliders)
            {
                if (doOnX)
                {
                    for (float t = 0; t < hitbox.Width; t += 8)
                    {
                        Vector2 vecTop = Position + hitbox.Position + new Vector2(t, -1);
                        Vector2 vecBottom = Position + hitbox.Position + new Vector2(t, hitbox.Height + 1);
                        if (SourceSceneCollision.CheckPoint(((ISourceScene)Scene).SourceScene.Entities, "Celeste.Solid",
                                vecTop))
                            level.ParticlesFG.Emit(
                                ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.ZipMover.P_Scrape,
                                vecTop);
                        if (SourceSceneCollision.CheckPoint(((ISourceScene)Scene).SourceScene.Entities, "Celeste.Solid",
                                vecBottom))
                            level.ParticlesFG.Emit(
                                ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.ZipMover.P_Scrape,
                                vecBottom);
                    }
                }

                if (doOnY)
                {
                    for (float t = 0; t < hitbox.Height; t += 8)
                    {
                        Vector2 vecLeft = Position + hitbox.Position + new Vector2(-1, t);
                        Vector2 vecRight = Position + hitbox.Position + new Vector2(hitbox.Width + 1, t);
                        if (SourceSceneCollision.CheckPoint(((ISourceScene)Scene).SourceScene.Entities, "Celeste.Solid",
                                vecLeft))
                            level.ParticlesFG.Emit(
                                ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.ZipMover.P_Scrape,
                                vecLeft);
                        if (SourceSceneCollision.CheckPoint(((ISourceScene)Scene).SourceScene.Entities, "Celeste.Solid",
                                vecRight))
                            level.ParticlesFG.Emit(
                                ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.ZipMover.P_Scrape,
                                vecRight);
                    }
                }
            }

            Collidable = true;
        }

        public void SpawnScrapeParticlesExcluding(Type[] ignores, bool doOnX = true, bool doOnY = true)
        {
            Collidable = false;

            OriginalLevelSceneRoot level = (OriginalLevelSceneRoot)Scene;
            foreach (Hitbox hitbox in Colliders)
            {
                if (doOnX)
                {
                    for (float t = 0; t < hitbox.Width; t += 8)
                    {
                        Vector2 vecTop = Position + hitbox.Position + new Vector2(t, -1);
                        Vector2 vecBottom = Position + hitbox.Position + new Vector2(t, hitbox.Height + 1);
                        if (CollideSolidExcluding(ignores, vecTop))
                            level.ParticlesFG.Emit(
                                ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.ZipMover.P_Scrape,
                                vecTop);
                        if (CollideSolidExcluding(ignores, vecBottom))
                            level.ParticlesFG.Emit(
                                ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.ZipMover.P_Scrape,
                                vecBottom);
                    }
                }

                if (doOnY)
                {
                    for (float t = 0; t < hitbox.Height; t += 8)
                    {
                        Vector2 vecLeft = Position + hitbox.Position + new Vector2(-1, t);
                        Vector2 vecRight = Position + hitbox.Position + new Vector2(hitbox.Width + 1, t);
                        if (CollideSolidExcluding(ignores, vecLeft))
                            level.ParticlesFG.Emit(
                                ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.ZipMover.P_Scrape,
                                vecLeft);
                        if (CollideSolidExcluding(ignores, vecRight))
                            level.ParticlesFG.Emit(
                                ((IOriginalGameplayProcess)SourceWorld.EngineContext).ParticleTypes.ZipMover.P_Scrape,
                                vecRight);
                    }
                }
            }

            Collidable = true;
        }

        public void AddLightOccluders(float alpha = 1f)
        {
            foreach (Hitbox hitbox in Colliders)
                Add(new LightOccludeState(
                    new Rectangle((int)hitbox.Position.X, (int)hitbox.Position.Y, (int)hitbox.Width,
                        (int)hitbox.Height),
                    alpha));
        }

        private bool TileCollideWithGroup(int x, int y)
        {
            return SourceEntityGeometryCollision.Rectangle(this,
                new Rectangle((int)GroupBoundsMin.X + (x * 8), (int)GroupBoundsMin.Y + (y * 8), 8, 8));
        }

        private static int ToBitFlag(bool a, bool b, bool c, bool d)
            => (a ? 1 : 0) | (b ? 2 : 0) | (c ? 4 : 0) | (d ? 8 : 0);

        private bool CollideSolidExcluding(Type[] ignores, Vector2 point)
        {
            foreach (var entry in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities("Celeste.Solid"))
            {
                var entity = (SceneEntityState)entry;
                if (entity.Collidable && !ignores.Contains(entity.GetType()) &&
                    SourceEntityGeometryCollision.Point(entity, point))
                    return true;
            }

            return false;
        }

        public bool IsGroupVisible() => IsGroupVisibleAt(Position);

        public bool IsGroupVisibleAt(Vector2 pos)
        {
            Rectangle bounds = GetGroupBoundsAt(pos);
            return EverestCullHelper.IsRectangleVisible(SourceWorld.EngineContext, bounds.X, bounds.Y, bounds.Width,
                bounds.Height);
        }
    }
}
