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

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    public partial class CustomCassetteBlock
    {
        protected override void FindInGroup(CassetteBlockState block)
        {
            if (Lonely) return;
            foreach (CassetteBlockState entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         "Celeste.CassetteBlock"))
            {
                bool oldPredicate = entity != this && entity != block && entity.Index == Index
                                    && (SourceEntityGeometryCollision.Rectangle(entity,
                                            new Rectangle((int)block.X - 1, (int)block.Y, (int)block.Width + 2,
                                                (int)block.Height))
                                        || SourceEntityGeometryCollision.Rectangle(entity,
                                            new Rectangle((int)block.X, (int)block.Y - 1, (int)block.Width,
                                                (int)block.Height + 2)))
                                    && !group.Contains(entity);
                bool newPredicate = oldPredicate && entity.Color == base.color &&
                                    !(entity is CustomCassetteBlock custom && custom.Lonely);
                if (oldConnectionBehavior ? oldPredicate : newPredicate)
                {
                    group.Add(entity);
                    FindInGroup(entity);
                    AssignGroupTo(entity);
                }
            }
        }

        protected override bool CheckForSame(float x, float y)
        {
            foreach (CassetteBlockState entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(
                         "Celeste.CassetteBlock"))
            {
                bool oldPredicate = entity.Index == Index &&
                                    SourceEntityGeometryCollision.Rectangle(entity.Collider,
                                        new Rectangle((int)x, (int)y, 8, 8));
                bool newPredicate = oldPredicate && entity.Color == base.color &&
                                    !(entity is CustomCassetteBlock custom && custom.Lonely);
                if (Lonely)
                    return SourceEntityGeometryCollision.Rectangle(Collider, new Rectangle((int)x, (int)y, 8, 8));
                if (oldConnectionBehavior ? oldPredicate : newPredicate) return true;
            }

            return false;
        }

        protected override void SetImage(float x, float y, int tx, int ty)
        {
            var frames =
                GameAtlas.GetAtlasSubtextures(
                    SuffixFromSpritePathOrDefault("/pressed", "objects/cassetteblock/pressed"));
            pressed.Add(CreateImage(x, y, tx, ty, frames[Index % frames.Count]));
            solid.Add(CreateImage(x, y, tx, ty,
                GameAtlas[SuffixFromSpritePathOrDefault("/solid", "objects/cassetteblock/solid")]));
        }
    }
}
