using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.Platforms.ZipMovers;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Rendering;
using Celeste.Mods.CommunalHelper.Content.Stations;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Rectangle = Celeste.Core.Geometry.IntRect;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;

namespace Celeste.Mods.CommunalHelper.Entities.Stations
{
    public partial class StationBlockTrack : SceneEntityState
    {
        public override void Update()
        {
            base.Update();

            if (MasterOfGroup)
            {
                foreach (Node node in Track)
                {
                    if (node.HasIndicator && node.ColorLerp != 0f)
                    {
                        node.ColorLerp = ScalarMath.Approach(node.ColorLerp, 0f, Engine.DeltaTime);
                    }
                }
            }

            trackStatePercent +=
                ((switchState is TrackSwitchState.On or TrackSwitchState.None ? 0f : 1f) - trackStatePercent) / 4 *
                Engine.DeltaTime * 25;
        }

        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);

            if (MasterOfGroup)
            {
                foreach (StationBlockTrack track in Group)
                {
                    track.DrawPipe(context);
                }

                float bounce = (float)Math.Floor(1.5f * (((ISourceScene)Scene).SourceScene.Clock.TimeActive % 1f));

                foreach (Node node in Track)
                {
                    int frame = (int)(node.Percent * 8) % nodeSprite.Count; // Allows for somewhat speed control.
                    SourceAtlasDrawing.DrawCentered(context, this, nodeSprite[frame], node.Center, Color.White);
                    if (node.HasIndicator && node.PushForce != Vector2.Zero)
                    {
                        SourceAtlasDrawing.DrawCentered(context, this, forceArrow,
                            node.Center + (node.PushForce * (8f + bounce)),
                            MixColor(node.IndicatorColor, node.IndicatorIncomingColor, node.ColorLerp), 1f,
                            (float)Math.Atan2(node.PushForce.Y, node.PushForce.X));
                    }
                }
            }
        }

        private void DrawPipe(ISourceRenderContext context)
        {
            if (switchState != TrackSwitchState.None)
            {
                for (int i = 0; i <= length; i += 8)
                {
                    SourceAtlasDrawing.Draw(context, this, disabledTrackSprite,
                        Position + new Vector2(Horizontal ? i : 0, Horizontal ? 0 : i), Vector2.Zero,
                        Color.White * trackStatePercent);
                }
            }

            int trackpercent = (int)(trackStatePercent * (length + 2));

            for (int i = (int)ScalarMath.Mod(
                             trackConstantLooping
                                 ? ((ISourceScene)Scene).SourceScene.Clock.TimeActive * 14
                                 : TrackOffset,
                             8) +
                         trackpercent;
                 i <= length;
                 i += 8)
            {
                SourceAtlasDrawing.Draw(context, this, trackSprite,
                    Position + new Vector2(Horizontal ? i : 0, Horizontal ? 0 : i));
            }
        }

        private Color MixColor(Color a, Color b, float t)
        {
            t = ScalarMath.Clamp(t, 0f, 1f);
            var m = ((ISourceScalarEnvironment)Engine).ScalarArithmetic;
            return new Color((int)m.Lerp(a.R, b.R, t), (int)m.Lerp(a.G, b.G, t), (int)m.Lerp(a.B, b.B, t),
                (int)m.Lerp(a.A, b.A, t));
        }

        public void CreateSparks(Vector2 position, ParticleType p)
            => ZipMoverDrawing.Sparks(this, Engine, position, position, sparkAdd, sparkDirFromA, sparkDirFromB,
                sparkDirToA,
                sparkDirToB, p);
    }
}
