using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Audio;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Gameplay.Visibility;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Graphics.Rendering.Drawing;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Particles;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Geometry.TwoD.Camera;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Presentation.PostProcessing.Displacement;
using Color = Celeste.Core.Display.PackedColor;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    public partial class ConnectedDreamBlock
    {
        public override void BeginShatter()
        {
            if (ShatterCheck())
            {
                ((ISourceSpatialAudioEnvironment)SourceEngine).SpatialAudio.Play(
                    "event:/CommunalHelperEvents/game/connectedDreamBlock/dreamblock_shatter", Position);

                ConnectedDreamBlock master = MasterOfGroup ? this : this.master;
                foreach (ConnectedDreamBlock block in master.Group)
                {
                    block.shattering = true;
                    block.Add(new SimulationCoroutine(SourceEngine, block.ShatterSequence()));
                }
            }
        }

        private IEnumerator ShatterSequence()
        {
            if (QuickDestroy)
            {
                Collidable = false;
                foreach (SourceStaticMoverState entity in staticMovers)
                    ((SceneEntityState)entity.Entity).Collidable = false;
            }
            else
                yield return 0.28f;

            while (ColorLerp < 2.0f)
            {
                ColorLerp += SourceEngine.DeltaTime * 10.0f;
                yield return null;
            }

            ColorLerp = 1.0f;
            if (!QuickDestroy)
                yield return 0.05f;

            if (MasterOfGroup)
            {
                OriginalLevelSceneRoot level = ((OriginalLevelSceneRoot)Scene);
                level.Shake(.65f);
                Vector2 camera = level.Camera.Position;

                Vector2 centre = new(GroupRect.Center.X, GroupRect.Center.Y);
                for (int i = 0; i < particles.Length; i++)
                {
                    Vector2 position = particles[i].Position;
                    position += camera * (0.3f + (0.25f * particles[i].Layer));
                    position = PutInside(position, GroupRect);
                    bool inside = false;
                    foreach (ConnectedDreamBlock block in Group)
                    {
                        if (SourceEntityGeometryCollision.Point(block, position))
                        {
                            inside = true;
                            break;
                        }
                    }

                    if (!inside)
                        continue;

                    Color flickerColor = Color.Lerp(particles[i].Color, Color.White, 0.6f);
                    ParticleType type = new(((IOriginalGameplayProcess)SourceEngine).ParticleTypes.Lightning.P_Shatter)
                    {
                        ColorMode = ParticleType.ColorModes.Fade,
                        Color = particles[i].Color,
                        Color2 = flickerColor, //Color.Lerp(particles[i].Color, Color.White, 0.5f),
                        Source = FeatherMode ? featherTextures[particles[i].Layer] : particleTextures[2],
                        SpinMax = FeatherMode ? (float)Math.PI : 0,
                        RotationMode =
                            FeatherMode ? ParticleType.RotationModes.Random : ParticleType.RotationModes.None,
                        Direction = Vector2Geometry.Angle(position - centre)
                    };
                    level.ParticlesFG.Emit(type, 1, position, Vector2.One * 3f);
                }

                foreach (ConnectedDreamBlock block in Group)
                    block.OneUseDestroy();

                foreach (SourceJumpThruEntityState jumpThru in JumpThrus)
                    jumpThru.RemoveSelf();

                SourceEngine.Effects.Glitch = 0.22f;
                while (SourceEngine.Effects.Glitch > 0.0f)
                {
                    SourceEngine.Effects.Glitch -= 0.5f * SourceEngine.DeltaTime;
                    yield return null;
                }

                SourceEngine.Effects.Glitch = 0.0f;
            }

            RemoveSelf();
        }

        private static Vector2 PutInside(Vector2 pos, IntRect r)
        {
            while (pos.X < r.X)
                pos.X += r.Width;

            while (pos.X > r.X + r.Width)
                pos.X -= r.Width;

            while (pos.Y < r.Y)
                pos.Y += r.Height;

            while (pos.Y > r.Y + r.Height)
                pos.Y -= r.Height;

            return pos;
        }
    }
}
