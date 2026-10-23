using System.Collections;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;

namespace Celeste.Mods.CommunalHelper.Entities.Lightning
{
    internal sealed class LightningFlash : SceneEntityState
    {
        private readonly PackedColor color;
        private float alpha = 1f;
        private readonly float opacity;
        private readonly ISourceEngineContext engine;

        public LightningFlash(ISourceEngineContext engine, PackedColor color, float opacity, int depth, float time)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            this.color = color;
            Depth = depth;
            this.opacity = opacity;
            OwnedComponents.Add(new SimulationCoroutine(engine, FlashRoutine(time)));
        }

        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);
            var camera = ((OriginalLevelSceneRoot)Scene).Camera;
            SourcePrimitiveDrawing.Rect(context, this, camera.Position - Vector2.One,
                (int)camera.Right - (int)camera.Left + 2, (int)camera.Bottom - (int)camera.Top + 2,
                color * alpha * opacity);
        }

        private IEnumerator FlashRoutine(float time)
        {
            for (float t = time; t > 0f; t -= engine.DeltaTime)
            {
                alpha = t / time;
                yield return null;
            }

            alpha = 0f;
            RemoveSelf();
        }
    }
}
