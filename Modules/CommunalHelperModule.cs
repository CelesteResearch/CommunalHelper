// SPDX-License-Identifier: MIT

using System;
using Celeste.Core.Scheduling;
using Celeste.Mods.CommunalHelper.Runtime.DreamBlocks;
using Celeste.Everest.Modules.Runtime;
using Celeste.Graphics.Content;
using Celeste.Mods.CommunalHelper.Content;

namespace Celeste.Mods.CommunalHelper.Modules
{
    public sealed partial class CommunalHelperModule : SourceEverestModule
    {
        private global::Celeste.Mods.CommunalHelper.Backdrops.Clouds.CloudscapeResources cloudscapes;

        public override Type SessionType =>
            typeof(global::Celeste.Mods.CommunalHelper.Persistence.CommunalHelperSession);

        public global::Celeste.Mods.CommunalHelper.Persistence.CommunalHelperSession Session =>
            (global::Celeste.Mods.CommunalHelper.Persistence.CommunalHelperSession)_Session;

        private IDisposable customDreamBlocks;
        private IDisposable connectedDreamBlocks;
        internal ConnectedMoveBlockTextures Textures { get; private set; }

        public CommunalHelperModule(ISourceEngineContext engine) : base(engine)
        {
        }

        // This connected-block subset registers factories and components through the assembly registry.
        public override void Load()
        {
            try
            {
                LoadUnderwaterMusic();
                LoadPanels();
                LoadAttachedWallBoosters();
                LoadCassetteBlocks();
                LoadMoveBlockRedirects();
                LoadDreamRefills();
                LoadDreamTunnels();
                customDreamBlocks = CustomDreamBlockProcess.Install(SourceEngine);
                connectedDreamBlocks = ConnectedDreamBlockProcess.Install(SourceEngine);
                LoadDreamMoveBlocks();
                LoadCassetteJumpFix();
                LoadInputControllers();
            }
            catch
            {
                Unload();
                throw;
            }
        }

        public override void Unload()
        {
            UnloadUnderwaterMusic();
            UnloadPanels();
            UnloadAttachedWallBoosters();
            UnloadDreamMoveBlocks();
            UnloadInputControllers();
            UnloadCassetteMoveDebris();
            UnloadCassetteJumpFix();
            UnloadTimedTriggerSpikes();
            UnloadDreamRefills();
            UnloadDreamTunnels();
            connectedDreamBlocks?.Dispose();
            connectedDreamBlocks = null;
            customDreamBlocks?.Dispose();
            customDreamBlocks = null;
            UnloadMoveBlockRedirects();
            UnloadCassetteBlocks();
            Textures = null;
            cloudscapes?.Dispose();
            cloudscapes = null;
        }

        public override void LoadContent(bool firstLoad)
        {
            LoadCassetteMoveDebris();
            var stations = global::Celeste.Mods.CommunalHelper.Content.Stations.StationResources.For(SourceEngine);
            stations.LoadSpriteBank();
            stations.InitializeBlockParticles();
            stations.InitializeTrackTextures();
            stations.InitializeSwitchParticles();
            InitializeDreamRefillParticles();
            InitializeDreamTunnelParticles();
            InitializeDreamMoveParticles();
            Entities.DreamBlocks.DreamZipMover.InitializeTextures(SourceEngine);
            global::Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks.ConnectedZipMover.InitializeTextures(
                SourceEngine);
            cloudscapes ??= global::Celeste.Mods.CommunalHelper.Backdrops.Clouds.CloudscapeResources.For(SourceEngine);
            cloudscapes.Reload();
            var atlas = ((ISourceGameGraphicsEnvironment)SourceEngine).GameGraphics.GameAtlas;
            if (Textures == null) Textures = new ConnectedMoveBlockTextures(atlas);
            else Textures.Reload(atlas);
        }
    }
}
