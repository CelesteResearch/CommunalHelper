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
    [CustomEntity("CommunalHelper/StationBlockTrack")]
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.StationBlockTrack")]
    public partial class StationBlockTrack : SceneEntityState
    {
        private TrackMoveMode moveMode;
        private TrackMoveMode initialMoveMode;

        private TrackSwitchState switchState;
        private readonly TrackSwitchState initialSwitchState;

        public bool CanBeUsed => switchState != TrackSwitchState.Off;


        public bool HasGroup { get; private set; }
        public bool MasterOfGroup { get; private set; }
        public StationBlockTrack master;

        private Rectangle nodeRect1, nodeRect2, trackRect;

        private readonly Node initialNodeData1, initialNodeData2;
        private Node node1, node2;

        private List<Node> Track;
        private List<StationBlockTrack> Group;
        private readonly bool multiBlockTrack = false;
        private readonly bool dynamicRouting = false;

        public Vector2? OneWayDir;

        private MTexture trackSprite, disabledTrackSprite;
        private List<MTexture> nodeSprite;

        private readonly float sparkDirFromA, sparkDirFromB, sparkDirToA, sparkDirToB, length;
        public float Percent = 0f;
        private Vector2 from, to, sparkAdd;

        public bool Horizontal;

        private bool trackConstantLooping;
        private float trackStatePercent;
        public float TrackOffset = 0f;

        private static readonly string TracksPath = "objects/CommunalHelper/stationBlock/tracks/";

        private MTexture forceArrow => StationResources.For(Engine).ForceArrow;
        private readonly ISourceEngineContext Engine;
        private AtlasData Atlas => ((ISourceGameGraphicsEnvironment)Engine).GameGraphics.GameAtlas;

        public StationBlockTrack(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            Engine = engine;
            Depth = 5000;

            initialSwitchState = switchState = data.Enum("trackSwitchState", TrackSwitchState.None);
            initialMoveMode = moveMode = data.Enum("moveMode", TrackMoveMode.None);
            if (StationResources.For(Engine).Session.TrackInitialState == TrackSwitchState.Off &&
                initialSwitchState != TrackSwitchState.None)
                Switch(TrackSwitchState.Off);
            if (StationResources.For(Engine).Session.TrackInitialMoveMode == TrackMoveMode.BackwardForce)
            {
                moveMode = Invert(moveMode);
            }

            trackStatePercent = switchState is TrackSwitchState.On or TrackSwitchState.None ? 0f : 1f;

            Horizontal = data.Bool("horizontal");
            multiBlockTrack = data.Bool("multiBlockTrack", false);
            dynamicRouting = data.Bool("dynamicRouting", false);
            Collider = new Hitbox(Horizontal ? data.Width : 8, Horizontal ? 8 : data.Height);

            nodeRect1 = new Rectangle((int)X, (int)Y, 8, 8);
            nodeRect2 = new Rectangle((int)(X + Width - 8), (int)(Y + Height - 8), 8, 8);

            initialNodeData1 = new Node(nodeRect1.X, nodeRect1.Y);
            initialNodeData2 = new Node(nodeRect2.X, nodeRect2.Y);

            Color indicatorColor = StationResources.HexColorNullable(data, "indicatorColor") ?? Color.White;
            Color indicatorIncomingColor =
                StationResources.HexColorNullable(data, "indicatorIncomingColor") ?? Color.White;
            bool hasIndicator = data.Bool("indicator", true);

            Vector2 dir = Horizontal ? Vector2.UnitX : Vector2.UnitY;
            switch (moveMode)
            {
                case TrackMoveMode.ForwardForce:
                    initialNodeData1.PushForce = Horizontal ? Vector2.UnitX : Vector2.UnitY;
                    initialNodeData1.HasIndicator = hasIndicator;
                    if (hasIndicator)
                    {
                        initialNodeData1.IndicatorColor = indicatorColor;
                        initialNodeData1.IndicatorIncomingColor = indicatorIncomingColor;
                    }

                    OneWayDir = dir;
                    break;

                case TrackMoveMode.ForwardOneWay:
                    OneWayDir = dir;
                    break;

                case TrackMoveMode.BackwardForce:
                    initialNodeData2.PushForce = Horizontal ? -Vector2.UnitX : -Vector2.UnitY;
                    initialNodeData2.HasIndicator = hasIndicator;
                    if (hasIndicator)
                    {
                        initialNodeData2.IndicatorColor = indicatorColor;
                        initialNodeData2.IndicatorIncomingColor = indicatorIncomingColor;
                    }

                    OneWayDir = -dir;
                    break;

                case TrackMoveMode.BackwardOneWay:
                    OneWayDir = -dir;
                    break;

                default:
                    break;
            }

            if (Horizontal)
            {
                initialNodeData1.NodeRight = initialNodeData2;
                initialNodeData1.TrackRight = this;

                initialNodeData2.NodeLeft = initialNodeData1;
                initialNodeData2.TrackLeft = this;

                trackRect = new Rectangle((int)X + 8, (int)Y, (int)Width - 16, (int)Height);
                length = Width - 8;
            }
            else
            {
                initialNodeData1.NodeDown = initialNodeData2;
                initialNodeData1.TrackDown = this;

                initialNodeData2.NodeUp = initialNodeData1;
                initialNodeData2.TrackUp = this;

                trackRect = new Rectangle((int)X, (int)Y + 8, (int)Width, (int)Height - 16);
                length = Height - 8;
            }

            from = initialNodeData1.Center;
            to = initialNodeData2.Center;
            sparkAdd = Vector2Geometry.Perpendicular(
                ((ISourceVectorEnvironment)Engine).VectorArithmetic.SafeNormalize(from - to, 3f));

            float num = (float)Math.Atan2((from - to).Y, (from - to).X);
            sparkDirFromA = num + ((float)Math.PI / 8f);
            sparkDirFromB = num - ((float)Math.PI / 8f);
            sparkDirToA = num + (float)Math.PI - ((float)Math.PI / 8f);
            sparkDirToB = num + (float)Math.PI + ((float)Math.PI / 8f);
        }

        public static StationBlockTrack Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(level.EngineContext, data, offset);
    }
}
