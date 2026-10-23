using Celeste.Core.Display;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Scene.Entities;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Graphics.Display;
using Celeste.Graphics.Rendering;

namespace Celeste.Mods.CommunalHelper.Entities.Lightning
{
    public partial class ColoredLightningStrike : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly PackedColor color;
        private bool on;

        private float scale;

        private ISourceRandom rand;

        private float strikeHeight;

        private Node strike;

        public ColoredLightningStrike(Vector2 position, int seed, float height, ISourceEngineContext engine,
            PackedColor color, int depth, float delay = 0f)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            Position = position;
            base.Depth = depth;
            this.color = color;
            this.engine = engine;
            rand = ((ISourceRandomProcessEnvironment)engine).RandomProcess.Factory.CreateSeeded(seed);
            strikeHeight = height;
            OwnedComponents.Add(new SimulationCoroutine(engine, Routine(delay)));
        }

        private IEnumerator Routine(float delay)
        {
            if (delay > 0f)
            {
                yield return delay;
            }

            scale = 1f;
            GenerateStikeNodes(-1, 10f);
            for (int j = 0; j < 5; j++)
            {
                on = true;
                yield return (1f - (float)j / 5f) * 0.1f;
                scale -= 0.2f;
                on = false;
                strike.Wiggle(rand);
                yield return 0.01f;
            }

            RemoveSelf();
        }

        private void GenerateStikeNodes(int direction, float size, Node parent = null)
        {
            if (parent == null)
            {
                parent = (strike = new Node(0f, 0f, size));
            }

            if (!(parent.Position.Y >= strikeHeight))
            {
                float num = direction * SourceRandomMath.Range(rand, -8, 20);
                float num2 = SourceRandomMath.Range(rand, 8, 16);
                float size2 = (0.25f + (1f - (parent.Position.Y + num2) / strikeHeight) * 0.75f) * size;
                Node node = new Node(parent.Position + new Vector2(num, num2), size2);
                parent.Children.Add(node);
                GenerateStikeNodes(direction, size, node);
                if (SourceRandomMath.NextFloat(rand) < 0.1f)
                {
                    Node node2 = new Node(parent.Position + new Vector2(0f - num, num2 * 1.5f), size2);
                    parent.Children.Add(node2);
                    GenerateStikeNodes(-direction, size, node2);
                }
            }
        }

        public override void Render(ISourceRenderContext context)
        {
            if (on)
            {
                strike.Render(context, this, ((ISourceVectorEnvironment)engine).VectorArithmetic, Position, scale,
                    color);
            }
        }
    }
}
