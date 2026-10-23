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

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedZipMover
    {
        public override void Render(ISourceRenderContext context)
        {
            if (!IsGroupVisible())
                return;

            Rectangle cameraBounds = Scene is OriginalLevelSceneRoot currentLevel ? CameraBounds(currentLevel) : new();

            Vector2 originalPosition = Position;
            Position += Shake;

            foreach (Hitbox extension in Colliders)
            {
                if (!cameraBounds.Intersects(extension.Bounds))
                    continue;

                if (theme == Themes.Moon)
                {
                    SourcePrimitiveDrawing.Rect(context, this, extension.Left + 2f + X, extension.Top + Y,
                        extension.Width - 4f, extension.Height, backgroundColor);
                    SourcePrimitiveDrawing.Rect(context, this, extension.Left + X, extension.Top + 2f + Y,
                        extension.Width,
                        extension.Height - 4f, backgroundColor);
                    foreach (Image t in InnerCornerTiles)
                        SourcePrimitiveDrawing.Rect(context, this, t.X + X, t.Y + Y, 8, 8, backgroundColor);
                }
                else
                    SourcePrimitiveDrawing.Rect(context, this, extension.Left + X, extension.Top + Y, extension.Width,
                        extension.Height, backgroundColor);
            }

            int n = 1;
            float rot = 0f;
            int count = innerCogs.Count;

            float w = GroupBoundsMax.X - GroupBoundsMin.X;
            float h = GroupBoundsMax.Y - GroupBoundsMin.Y;
            Vector2 offset = new Vector2(-8, -8) + GroupOffset;

            for (int i = 4; i <= h + 4; i += 8)
            {
                int oldN = n;
                for (int j = 4; j <= w + 4; j += 8)
                {
                    int index = ZipMoverDrawing.CogFrameIndex(rot, n, percent, count);
                    MTexture mTexture = innerCogs[index];
                    Vector2 zero = Vector2.Zero;

                    int x = (j - 4) / 8;
                    int y = (i - 4) / 8;
                    if (GroupTiles[x, y])
                    {
                        mTexture = ZipMoverDrawing.ClipInnerCog(mTexture, mTexture.FrameWidth, mTexture.FrameHeight,
                            !GroupTiles[x - 1, y], !GroupTiles[x + 1, y], !GroupTiles[x, y - 1], !GroupTiles[x, y + 1],
                            temp, out zero);
                        SourceAtlasDrawing.DrawCentered(context, this, mTexture,
                            Position + new Vector2(j, i) + zero + offset, Color.White * ((n < 0) ? 0.5f : 1f));
                    }

                    n *= -1;
                    rot += (float)Math.PI / 3f;
                }

                // Ensures the checkboard pattern for innercogs
                if (oldN == n)
                    n *= -1;
            }

            base.Render(context);
            Position = originalPosition;
        }

        public void DrawBorder(ISourceRenderContext context)
        {
            if (!drawBlackBorder)
                return;

            if (!IsGroupVisible())
                return;

            foreach (Hitbox extension in AllColliders)
            {
                SourcePrimitiveDrawing.HollowRect(context, this,
                    new Rectangle(
                        (int)(X + extension.Left - 1f + Shake.X),
                        (int)(Y + extension.Top - 1f + Shake.Y),
                        (int)extension.Width + 2,
                        (int)extension.Height + 2
                    ),
                    Color.Black
                );
            }
        }
    }
}
