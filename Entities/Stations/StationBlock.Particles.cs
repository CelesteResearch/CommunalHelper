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
        private void ScrapeParticlesCheck(Vector2 to)
        {
            if (!SceneClock.OnInterval(0.03f, Engine))
            {
                return;
            }

            bool flag = to.Y != ExactPosition.Y;
            bool flag2 = to.X != ExactPosition.X;
            if (flag && !flag2)
            {
                int num = Math.Sign(to.Y - ExactPosition.Y);
                Vector2 value = (num != 1) ? TopLeft : BottomLeft;
                int num2 = 4;
                if (num == 1)
                {
                    num2 = Math.Min((int)Height - 12, 20);
                }

                int num3 = (int)Height;
                if (num == -1)
                {
                    num3 = Math.Max(16, (int)Height - 16);
                }

                if (SourceSceneCollision.CheckPoint(Level.Scheduler.Entities, "Celeste.Solid",
                        value + new Vector2(-2f, num * -2)))
                {
                    for (int i = num2; i < num3; i += 8)
                    {
                        Level.ParticlesFG.Emit(((IOriginalGameplayProcess)Engine).ParticleTypes.ZipMover.P_Scrape,
                            TopLeft + new Vector2(0f, i + (num * 2f)),
                            (num == 1) ? (-(float)Math.PI / 4f) : ((float)Math.PI / 4f));
                    }
                }

                if (SourceSceneCollision.CheckPoint(Level.Scheduler.Entities, "Celeste.Solid",
                        value + new Vector2(Width + 2f, num * -2)))
                {
                    for (int j = num2; j < num3; j += 8)
                    {
                        Level.ParticlesFG.Emit(((IOriginalGameplayProcess)Engine).ParticleTypes.ZipMover.P_Scrape,
                            TopRight + new Vector2(-1f, j + (num * 2f)),
                            (num == 1) ? ((float)Math.PI * -3f / 4f) : ((float)Math.PI * 3f / 4f));
                    }
                }
            }
            else
            {
                if (!flag2 || flag)
                {
                    return;
                }

                int num4 = Math.Sign(to.X - ExactPosition.X);
                Vector2 value2 = (num4 != 1) ? TopLeft : TopRight;
                int num5 = 4;
                if (num4 == 1)
                {
                    num5 = Math.Min((int)Width - 12, 20);
                }

                int num6 = (int)Width;
                if (num4 == -1)
                {
                    num6 = Math.Max(16, (int)Width - 16);
                }

                if (SourceSceneCollision.CheckPoint(Level.Scheduler.Entities, "Celeste.Solid",
                        value2 + new Vector2(num4 * -2, -2f)))
                {
                    for (int k = num5; k < num6; k += 8)
                    {
                        Level.ParticlesFG.Emit(((IOriginalGameplayProcess)Engine).ParticleTypes.ZipMover.P_Scrape,
                            TopLeft + new Vector2(k + (num4 * 2f), -1f),
                            (num4 == 1) ? ((float)Math.PI * 3f / 4f) : ((float)Math.PI / 4f));
                    }
                }

                if (SourceSceneCollision.CheckPoint(Level.Scheduler.Entities, "Celeste.Solid",
                        value2 + new Vector2(num4 * -2, Height + 2f)))
                {
                    for (int l = num5; l < num6; l += 8)
                    {
                        Level.ParticlesFG.Emit(((IOriginalGameplayProcess)Engine).ParticleTypes.ZipMover.P_Scrape,
                            BottomLeft + new Vector2(l + (num4 * 2f), 0f),
                            (num4 == 1) ? ((float)Math.PI * -3f / 4f) : (-(float)Math.PI / 4f));
                    }
                }
            }
        }
    }
}
