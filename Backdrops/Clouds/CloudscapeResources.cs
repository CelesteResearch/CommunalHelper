using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Runtime.CompilerServices;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Geometry;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Content.Textures;
using Celeste.Formats.Graphics.Atlas;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.VirtualAssets;
using Celeste.Graphics.Rendering.Textures;
using Celeste.Graphics.Rendering.Devices;
using Celeste.Graphics.Rendering.Effects;

namespace Celeste.Mods.CommunalHelper.Backdrops.Clouds
{
    public sealed class CloudscapeResources : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, CloudscapeResources> Owners = new();

        public static CloudscapeResources For(ISourceEngineContext engine) =>
            Owners.GetValue(engine, static e => new(e));

        private readonly ISourceEngineContext engine;
        public AtlasData Atlas { get; private set; }
        public AtlasEntry[] CloudTextures { get; private set; }
        public SourceEffect Effect { get; private set; }
        private VirtualRenderTarget fullscreen, small;
        private CloudscapeResources(ISourceEngineContext engine) => this.engine = engine;

        public void Load()
        {
            if (Effect != null) return;
            try
            {
                var content = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry;
                const string directory = "CommunalHelper:/Graphics/Atlases/CommunalHelper/Cloudscape/";
                var xml = new XmlDocument();
                using (var stream = new MemoryStream(content.Get(directory + "atlas").Data)) xml.Load(stream);
                var environment = (ISourceAtlasLoaderEnvironment)engine;
                var construction = environment.AtlasConstruction;
                Atlas = construction.CreateAtlas();
                Atlas.Sources = new List<VirtualTexture>();
                foreach (XmlElement page in xml["atlas"])
                {
                    var asset = content.Get(directory + page.GetAttribute("n"));
                    var texture = ((EverestVirtualContent)environment.VirtualContent).CreateTexture(asset);
                    var source = construction.CreateEntry(texture, Atlas);
                    Atlas.Sources.Add(texture);
                    foreach (XmlElement image in page)
                    {
                        string name = image.GetAttribute("n");
                        int Number(string key) => int.Parse(image.GetAttribute(key));
                        var clip = new IntRect(Number("x"), Number("y"), Number("w"), Number("h"));
                        Atlas.Entries[name] = image.HasAttribute("fx")
                            ? construction.CreateEntry(source, name, clip, new Vector2(-Number("fx"), -Number("fy")),
                                Number("fw"), Number("fh"))
                            : construction.CreateEntry(source, name, clip);
                    }
                }

                CloudTextures = Atlas.GetAtlasSubtextures(string.Empty).ToArray();
                Effect = new SourceEffect(((ISourceGraphicsDeviceEnvironment)engine).GraphicsDevice,
                    content.Get("CommunalHelper:/Effects/CommunalHelper/cloudscape.cso").Data);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public VirtualRenderTarget GetBuffer(bool staySame, VirtualRenderTarget gameplay)
        {
            var content = ((ISourceVirtualContentEnvironment)engine).VirtualContent;
            if (staySame || gameplay == null || gameplay.Width == 320)
            {
                if (small is { IsDisposed: true }) small = null;
                return small ??=
                    content.CreateRenderTarget("communal_helper/shared_cloudscape_buffer_320x180", 320, 180);
            }

            if (fullscreen == null || fullscreen.IsDisposed || fullscreen.Width != gameplay.Width)
            {
                fullscreen?.Dispose();
                fullscreen = content.CreateRenderTarget("communal_helper/shared_cloudscape_buffer", gameplay.Width,
                    gameplay.Height);
            }

            return fullscreen;
        }

        public void Reload()
        {
            Atlas?.Dispose();
            Atlas = null;
            Effect?.Dispose();
            Effect = null;
            CloudTextures = null;
            Load();
        }

        public void Dispose()
        {
            fullscreen?.Dispose();
            fullscreen = null;
            small?.Dispose();
            small = null;
            Atlas?.Dispose();
            Atlas = null;
            Effect?.Dispose();
            Effect = null;
            CloudTextures = null;
            Owners.Remove(engine);
        }
    }
}
