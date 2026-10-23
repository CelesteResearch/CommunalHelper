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

namespace Celeste.Mods.CommunalHelper.Entities.Stations
{
    public partial class StationBlock
    {
        public override void Render(ISourceRenderContext context)
        {
            Vector2 position = Position;
            Position += Shake;

            int tileWidth = (int)(Width / 8f);
            int tileHeight = (int)(Height / 8f);

            bool inverted = TimedSpikeCapabilities.For(Engine).IsPlayerInverted?.Invoke() ?? false;
            bool playerOnTop = HasPlayerOnTop();
            float buttonPos = Y - 4 + (allowWavedash && playerOnTop && !inverted ? 1 : 0);
            float bottomButtonPos = Bottom - 4 - (allowWavedashBottom && playerOnTop && inverted ? 1 : 0);

            Vector2 renderOffset = (Vector2.One * 4f) + hitOffset;

            for (int i = 0; i < tileWidth; i++)
            {
                for (int j = 0; j < tileHeight; j++)
                {
                    Vector2 vec = new Vector2(X + (i * 8), Y + (j * 8)) + renderOffset;
                    vec = Center + ((vec - Center) * scale);

                    // Button rendering
                    if (allowWavedash && j == 0)
                    {
                        int tx = i == 0 ? 0 : (i == tileWidth - 1 ? 2 : 1);
                        Vector2 pos = new Vector2(X + (i * 8), buttonPos) + renderOffset;
                        pos = Center + ((pos - Center) * scale);
                        Color c = MixColor(buttonColor, buttonPressedColor, colorLerp);
                        SourceAtlasDrawing.DrawCentered(context, this, buttonTiles[tx, 0], pos, c, scale);
                        SourceAtlasDrawing.DrawCentered(context, this, buttonTiles[tx, 1], pos, c, scale);
                    }

                    // Bottom button rendering
                    if (allowWavedashBottom && j == tileHeight - 1)
                    {
                        int tx = i == 0 ? 0 : (i == tileWidth - 1 ? 2 : 1);
                        Vector2 pos = new Vector2(X + (i * 8), bottomButtonPos) + renderOffset;
                        pos = Center + ((pos - Center) * scale);
                        Color c = MixColor(buttonColor, buttonPressedColor, bottomColorLerp);
                        SourceAtlasDrawing.DrawCentered(context, this, buttonTiles[tx, 0], pos, c, scale, 0f, false,
                            true);
                        SourceAtlasDrawing.DrawCentered(context, this, buttonTiles[tx, 1], pos, c, scale, 0f, false,
                            true);
                    }

                    SourceAtlasDrawing.DrawCentered(context, this, blockTiles[i, j], vec, Color.White, scale);
                }
            }

            base.Render(context);
            Position = position;
        }
    }
}
