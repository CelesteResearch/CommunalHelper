using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Display;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Graphics.Display.Colors;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Mods.CommunalHelper.Entities.Stations;
using Celeste.Mods.CommunalHelper.Modules;
using Celeste.Mods.CommunalHelper.Persistence;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.CommunalHelper.Content.Stations
{
    public sealed class StationResources
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, StationResources> Owners = new();
        public static StationResources For(ISourceEngineContext engine) => Owners.GetValue(engine, static e => new(e));
        private readonly ISourceEngineContext engine;
        private StationResources(ISourceEngineContext engine) => this.engine = engine;
        public SpriteBank SpriteBank { get; private set; }
        public ParticleTypePresentation P_BlueSparks { get; private set; }
        public ParticleTypePresentation P_PurpleSparks { get; private set; }
        public ParticleTypePresentation P_Smash { get; private set; }
        public AtlasEntry ForceArrow { get; private set; }
        public StationBlockTrack.TrackSwitchState LocalTrackSwitchState;

        public CommunalHelperSession Session => ((ISourceEverestModulesEnvironment)engine).Modules
            .OfType<CommunalHelperModule>().Single().Session;

        public void LoadSpriteBank()
        {
            var content = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry;
            var xml = new XmlDocument();
            using (var stream = content.Get("CommunalHelper:/Graphics/CommunalHelper/Sprites").Stream) xml.Load(stream);
            SpriteBank = new SpriteBank(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, xml);
        }

        public void InitializeBlockParticles()
        {
            var sparks = ((IOriginalGameplayProcess)engine).ParticleTypes.ZipMover.P_Sparks;
            P_BlueSparks = new ParticleTypePresentation(sparks) { Color = PackedColor.FromHex("30a0e6") };
            P_PurpleSparks = new ParticleTypePresentation(sparks) { Color = PackedColor.FromHex("aa51d6") };
        }

        public void InitializeTrackTextures() => ForceArrow =
            ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[
                "objects/CommunalHelper/stationBlock/tracks/forceIndicator"];

        public void InitializeSwitchParticles() => P_Smash =
            new ParticleTypePresentation(((IOriginalGameplayProcess)engine).ParticleTypes.LightningBreakerBox.P_Smash)
                { Color = PackedColor.FromHex("ff4076"), Color2 = PackedColor.FromHex("57c7ff") };

        public static PackedColor? HexColorNullable(EntityData data, string key)
        {
            if (data.Values.TryGetValue(key, out var value))
            {
                string text = value.ToString();
                if (text.Length is 6 or 7) return SourceHexColor.HexToColor(text);
            }

            return null;
        }

        public static IEnumerable<T> Tracked<T>(object scene)
        {
            var engine = ((ISourceEngineContextScene)scene).EngineContext;
            var catalog = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog;
            return ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities(catalog.Find(typeof(T)).RegistryKey)
                .Cast<T>();
        }
    }
}
