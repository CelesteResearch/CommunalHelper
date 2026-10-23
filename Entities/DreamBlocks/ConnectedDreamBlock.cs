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
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Content.Maps.Entities;
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
    [CustomEntity("CommunalHelper/ConnectedDreamBlock")]
    [SceneTracked(true)]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.ConnectedDreamBlock")]
    public partial class ConnectedDreamBlock : CustomDreamBlock
    {
        private readonly struct SpaceJamTile
        {
            public readonly int X, Y;
            public readonly int[] Edges;
            public readonly bool Exist;

            public SpaceJamTile(int x, int y, bool exist)
            {
                X = x;
                Y = y;
                Edges = new int[4];
                for (int i = 0; i < Edges.Length; i++)
                    Edges[i] = -1;

                Exist = exist;
            }

            public bool EdgeExist(Edges edge)
            {
                return Edges[(int)edge] != -1;
            }

            public bool TryGetEdge(Edges edge, out int result)
            {
                result = Edges[(int)edge];
                return result != -1;
            }

            public int this[Edges edge]
            {
                get => Edges[(int)edge];
                set => Edges[(int)edge] = value;
            }
        }

        private struct SpaceJamEdge(Vector2 startV, Vector2 endV, float wobbleOff, bool flipNorm, Edges face)
        {
            public Vector2 start = startV, end = endV;
            public float wobbleOffset = wobbleOff;
            public bool flipNormal = flipNorm;
            public Edges facing = face;
        }

        private readonly struct SpaceJamCorner(
            int x,
            int y,
            bool ur,
            bool ul,
            bool dr,
            bool dl,
            bool iur,
            bool iul,
            bool idr,
            bool idl)
        {
            public readonly bool
                upright = ur,
                upleft = ul,
                downright = dr,
                downleft = dl,
                inupright = iur,
                inupleft = iul,
                indownright = idr,
                indownleft = idl;

            public readonly int x = x, y = y;
        }

        private List<SpaceJamEdge> GroupEdges;
        private List<SpaceJamCorner> GroupCorners;

        private IntRect GroupRect => new(
            (int)GroupBoundsMin.X,
            (int)GroupBoundsMin.Y,
            (int)(GroupBoundsMax.X - GroupBoundsMin.X),
            (int)(GroupBoundsMax.Y - GroupBoundsMin.Y));

        private enum Edges
        {
            North,
            East,
            South,
            West,
        }


        public Vector2 GroupBoundsMin;
        public Vector2 GroupBoundsMax;
        public Vector2 GroupOffset;

        public bool HasGroup { get; private set; }

        public bool MasterOfGroup { get; private set; }
        public Dictionary<SourcePlatformEntityState, Vector2> Moves;
        public List<ConnectedDreamBlock> Group;
        public List<SourceJumpThruEntityState> JumpThrus;
        protected ConnectedDreamBlock master;

        protected bool IncludeJumpThrus = false;

        internal bool OwnedBy(ISourceEngineContext owner) => ReferenceEquals(SourceEngine, owner);

        public static ConnectedDreamBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data)
            => new(level.EngineContext, data, offset, level.SolidWorldFactory);

        public ConnectedDreamBlock(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : base(engine, data, offset, worlds)
        {
        }
    }
}
