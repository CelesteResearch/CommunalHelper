using Celeste.Core.Components;
using Celeste.Graphics.Rendering.Drawing;
using Celeste.Formats.Map.Entities.Structure;
using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Gameplay.Visibility;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Color = Celeste.Core.Display.PackedColor;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;

namespace Celeste.Mods.CommunalHelper.Entities.Chains
{
    [CustomEntity("CommunalHelper/Chain")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.Chain")]
    public partial class Chain : SceneEntityState, ISourceEngineContextEntity
    {
        private readonly ISourceEngineContext engine;
        public ISourceEngineContext EngineContext => engine;
        private readonly ISourceVectorArithmetic arithmetic;
        private ISourceSpatialAudio SpatialAudio => ((ISourceSpatialAudioEnvironment)engine).SpatialAudio;
        public const string DEFAULT_CHAIN_PATH = "objects/CommunalHelper/chains/chain";

        private readonly bool outline;

        private readonly ChainNode[] nodes;
        private Func<Vector2> attachedStartGetter, attachedEndGetter;

        private readonly float distanceConstraint;

        public bool Tight;

        private readonly ISourceAudioEventReference sfx;
        private Vector2 sfxPos;

        private readonly MTexture texture;
        private readonly MTexture segment;

        private readonly bool updateOffscreen;
        private readonly Vector2[] oldNodePositions;

        public Chain(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine,
                data.Bool("outline", true),
                (int)((((ISourceVectorEnvironment)engine).VectorArithmetic.Distance(data.Position + offset,
                    data.NodesOffset(offset)[0]) / 8) + 1 + data.Int("extraJoints")),
                8,
                () => data.Position + offset,
                () => data.Nodes[0] + offset,
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas.GetOrDefault(
                    data.Attr("texture", DEFAULT_CHAIN_PATH),
                    ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[DEFAULT_CHAIN_PATH]),
                data.Bool("updateOffscreen", true)
            )
        {
        }

        public Chain(ISourceEngineContext engine, bool outline, int nodeCount, float distanceConstraint,
            Func<Vector2> attachedStartGetter, Func<Vector2> attachedEndGetter, MTexture texture, bool updateOffscreen)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            arithmetic = ((ISourceVectorEnvironment)engine).VectorArithmetic;
            Position = attachedStartGetter();
            this.texture = texture;
            segment = texture.GetSubtexture(0, 8, 8, 8);

            nodes = new ChainNode[nodeCount];
            this.attachedStartGetter = attachedStartGetter;
            this.attachedEndGetter = attachedEndGetter;
            this.distanceConstraint = distanceConstraint;
            this.updateOffscreen = updateOffscreen;

            this.outline = outline;

            Vector2 from = attachedStartGetter is not null ? attachedStartGetter() : Position;
            Vector2 to = attachedEndGetter is not null ? attachedEndGetter() : Position;
            for (int i = 0; i < nodes.Length; i++)
            {
                Vector2 newPos = from + arithmetic.Divide((to - from) * i, nodes.Length - 1);
                nodes[i].Position = newPos;
            }

            UpdateChain();
            sfx = ((ISourceAudioEnvironment)engine).SourceAudio.Play("event:/CommunalHelperEvents/game/chain/move");
            oldNodePositions = new Vector2[nodes.Length];
        }

        private void AttachedEndsToSolids(object scene)
        {
            Vector2 start = nodes[0].Position;
            Vector2 end = nodes[^1].Position;

            SceneEntityState startSolid = FirstSolid(scene, new IntRect((int)start.X - 2, (int)start.Y - 2, 4, 4));
            SceneEntityState endSolid = FirstSolid(scene, new IntRect((int)end.X - 2, (int)end.Y - 2, 4, 4));

            if (startSolid is not null)
            {
                Vector2 offset = start - startSolid.Position;
                attachedStartGetter = () => startSolid.Position + offset;
            }
            else
                attachedStartGetter = null;

            if (endSolid is not null)
            {
                Vector2 offset = end - endSolid.Position;
                attachedEndGetter = () => endSolid.Position + offset;
            }
            else
                attachedEndGetter = null;

            if (attachedStartGetter is null && attachedEndGetter is null)
                RemoveSelf();
        }

        private void UpdateSfx(Vector2[] oldPositions)
        {
            if (sfx is null)
                return;

            float intensity = 0f;
            if (nodes.Length > 0 && nodes.Length == oldPositions.Length && TryGetPlayer(out RoomPlayerSession player))
            {
                float minDistSqr = float.MaxValue;

                Vector2 averageOffset = Vector2.Zero;
                int eff = nodes.Length;
                for (int i = 0; i < nodes.Length; i++)
                {
                    float lengthSqr = arithmetic.LengthSquared(player.Center - nodes[i].Position);
                    if (lengthSqr < minDistSqr)
                    {
                        minDistSqr = lengthSqr;
                        sfxPos = nodes[i].Position;
                    }

                    Vector2 offset = nodes[i].Position - oldPositions[i];
                    if (arithmetic.LengthSquared(offset) < 0.025f && eff > 1)
                        eff--;
                    averageOffset += offset;
                }

                averageOffset = arithmetic.Divide(averageOffset, eff);

                intensity = Tight ? 0f : Math.Clamp(arithmetic.Length(averageOffset) / 2f, 0f, 1f);
            }

            sfx.SetParameter("intensity", intensity);
            SpatialAudio.Position(sfx, sfxPos);
        }

        private void RemoveSfx()
        {
            SpatialAudio.Stop(sfx);
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            RemoveSfx();
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            AttachedEndsToSolids(scene);
            if (attachedStartGetter is not null && attachedEndGetter is not null)
            {
                for (int _ = 0; _ < 80; _++)
                {
                    UpdateChain(true);
                }
            }
        }

        public override void Update()
        {
            base.Update();

            if (!updateOffscreen && !IsVisible())
                return;

            for (int i = 0; i < nodes.Length; i++)
                oldNodePositions[i] = nodes[i].Position;
            UpdateChain();
            UpdateSfx(oldNodePositions);

            if (arithmetic.Distance(nodes[0].Position, nodes[^1].Position) > (nodes.Length + 1) * distanceConstraint)
                BreakInHalf();
        }

        private void BreakInHalf()
        {
            RemoveSelf();
            Vector2 middleNode = nodes[nodes.Length / 2].Position;

            Chain a, b;
            ((ISourceScene)Scene).SourceScene.AddEntity(a = new Chain(engine, outline, nodes.Length / 2, 8,
                () => middleNode, attachedStartGetter, texture, updateOffscreen));
            a.AttachedEndsToSolids(Scene);
            a.ShakeImpulse();

            ((ISourceScene)Scene).SourceScene.AddEntity(b = new Chain(engine, outline, nodes.Length / 2, 8,
                () => middleNode, attachedEndGetter, texture, updateOffscreen));
            b.AttachedEndsToSolids(Scene);
            b.ShakeImpulse();

            SpatialAudio.Play("event:/CommunalHelperEvents/game/chainedFallingBlock/chain_tighten_block", middleNode);

            OriginalLevelSceneRoot level = (OriginalLevelSceneRoot)Scene;
            for (int i = 0; i < 10; i++)
                level.ParticlesFG.Emit(((IOriginalGameplayProcess)engine).ParticleTypes.ZipMover.P_Sparks, middleNode,
                    SourceRandomMath.NextAngle(((ISourceRandomEnvironment)engine).Random));
        }

        private bool IsVisible()
        {
            // Construct a rectangle containing the first, middle and last node, which acts as the bounding box of this chain.
            // Code adapted from CullHelper.IsCurveVisible
            Vector2 begin = nodes[0].Position;
            Vector2 control = nodes[nodes.Length / 2].Position;
            Vector2 end = nodes[^1].Position;

            float x = Math.Min(begin.X, Math.Min(control.X, end.X));
            float right = Math.Max(begin.X, Math.Max(control.X, end.X));
            float y = Math.Min(begin.Y, Math.Min(control.Y, end.Y));
            float bottom = Math.Max(begin.Y, Math.Max(control.Y, end.Y));

            return EverestCullHelper.IsRectangleVisible(engine, x, y, right - x, bottom - y,
                lenience: segment.FrameWidth + 2);
        }
    }
}
