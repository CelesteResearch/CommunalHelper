using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Settings;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.CommunalHelper.Entities.Lightning
{
    [CustomEntity("CommunalHelper/LightningController")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.LightningController")]
    [SceneTracked(false)]
    public sealed class LightningController : SceneEntityState
    {
        private readonly ISourceEngineContext engine;

        public LightningController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            OwnedComponents.Add(new SimulationCoroutine(engine, Routine(data.Float("minDelay", 5f),
                data.Float("maxDelay", 10f), data.Float("startupDelay", 4f), data.Float("flash", .3f),
                data.Float("flashDuration", .5f), data.Int("depth", 0), data.Float("shakeAmount", .3f),
                data.Attr("sfx", "event:/new_content/game/10_farewell/lightning_strike"), data.Float("probability", 1f),
                ParseColor(data, "color"), ParseColor(data, "flashColor"))));
        }

        private static PackedColor ParseColor(EntityData data, string key) =>
            Celeste.Graphics.Display.Colors.SourceHexColor.HexToColor(data.Attr(key, "ffffff"));

        private IEnumerator Routine(float minDelay, float maxDelay, float startupDelay, float flash,
            float flashDuration, int depth, float shakeAmount, string sfx, float probability, PackedColor color,
            PackedColor flashColor)
        {
            yield return startupDelay;
            if (probability == 0f) RemoveSelf();
            var level = (OriginalLevelSceneRoot)Scene;
            var cam = level.Camera;
            bool flashes = flashDuration > 0f && flash > 0f &&
                           ((IEverestCurrentCoreSettingsEnvironment)engine).CurrentCoreSettings.AllowLightning;
            var random = ((ISourceRandomProcessEnvironment)engine).RandomProcess.Proxy;
            while (true)
            {
                yield return minDelay + (maxDelay - minDelay) * SourceRandomMath.NextFloat(random);
                if (!(SourceRandomMath.NextFloat(random) >= probability))
                {
                    int width = (int)cam.Right - (int)cam.Left;
                    int height = (int)cam.Bottom - (int)cam.Top;
                    Vector2 at = cam.Position + new Vector2(width / 2, height / -2);
                    int seed = random.Next();
                    level.DirectionalShake(Vector2.UnitY, shakeAmount);
                    ((ISourceScene)level).SourceScene.AddEntity(new ColoredLightningStrike(at, seed, width, engine,
                        color, depth));
                    if (flashes)
                        ((ISourceScene)level).SourceScene.AddEntity(new LightningFlash(engine, flashColor, flash,
                            depth - 1, flashDuration));
                    ((ISourceAudioEnvironment)engine).SourceAudio.Play(sfx);
                }
            }
        }
    }
}
