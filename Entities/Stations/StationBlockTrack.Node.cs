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
        [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.StationBlockTrack+Node")]
        public class Node
        {
            public Node(int x, int y)
            {
                Position = new Vector2(x, y);
                Center = Position + new Vector2(4, 4);
                Hitbox = new Rectangle(x, y, 8, 8);
            }

            public Vector2 Position, Center;
            public Rectangle Hitbox;

            public Node NodeUp, NodeDown, NodeLeft, NodeRight;
            public StationBlockTrack TrackUp, TrackDown, TrackLeft, TrackRight;

            public Vector2 PushForce;
            public Color IndicatorColor;
            public Color IndicatorIncomingColor;
            public bool HasIndicator;
            public float ColorLerp;

            public float Percent = 0f;
        }
    }
}
