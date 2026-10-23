// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Numerics;
using Celeste.Everest.Runtime.Levels.Update;
using Celeste.Everest.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Content.Maps.Entities;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Mods.CommunalHelper.Entities.CassetteBlocks;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Intro;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.CassetteBlocks
{
    internal sealed class CustomCassetteBlockProcess : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, CustomCassetteBlockProcess> Owners = new();
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();
        private bool createdCassetteManager;
        private bool disposed;
        private readonly HashSet<string> entityNames = new(StringComparer.Ordinal);
        private EverestLevelUpdateEvents events;

        private CustomCassetteBlockProcess(ISourceEngineContext engine) => this.engine = engine;

        internal static IDisposable Install(ISourceEngineContext engine)
        {
            var process = new CustomCassetteBlockProcess(engine);
            Owners.Add(engine, process);
            try
            {
                process.hooks.Add(new ILHook(typeof(CassetteBlockState).GetMethod(nameof(CassetteBlockState.Update)),
                    il =>
                    {
                        var cursor = new ILCursor(il);
                        cursor.GotoNext(MoveType.After,
                            instruction => instruction.MatchLdfld<CassetteBlockState>("group"));
                        cursor.Emit(OpCodes.Ldarg_0);
                        cursor
                            .EmitDelegate<
                                Func<List<CassetteBlockState>, CassetteBlockState, List<CassetteBlockState>>>((
                                group, block) =>
                            {
                                if (block.Scene is OriginalLevelSceneRoot level &&
                                    ReferenceEquals(level.EngineContext, engine))
                                    group.RemoveAll(static member => member.Scene is null);
                                return group;
                            });
                    }));
                process.hooks.Add(new Hook(
                    typeof(SourcePlatformEntityState).GetMethod(nameof(SourcePlatformEntityState.EnableStaticMovers)),
                    (Action<Action<SourcePlatformEntityState>, SourcePlatformEntityState>)((original, platform) =>
                    {
                        if (platform is CustomCassetteBlock block && ReferenceEquals(block.EngineContext, engine) &&
                            !block.Visible) return;
                        original(platform);
                    })));
                process.hooks.Add(new Hook(
                    typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.LoadLevel)),
                    (Action<Action<OriginalLevelSceneRoot, PlayerIntroType, bool>, OriginalLevelSceneRoot,
                        PlayerIntroType,
                        bool>)((original, level, intro, fromLoader) =>
                    {
                        if (ReferenceEquals(level.EngineContext, engine)) process.createdCassetteManager = false;
                        original(level, intro, fromLoader);
                    })));
                process.events = ((IEverestLevelEntityLoadEnvironment)engine).LevelUpdateEvents;
                process.events.OnLoadEntity += process.OnLoadEntity;
                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        internal static void Initialize(ISourceEngineContext engine)
        {
            var owner = Owners.GetValue(engine,
                _ => throw new InvalidOperationException("The cassette runtime is not loaded."));
            owner.entityNames.Clear();
            foreach (var metadata in ((ISourceRuntimeTypeDiscoveryEnvironment)engine).RuntimeTypeDiscovery.GetTypes())
            {
                var type = metadata.RequireActualType();
                if (!typeof(CustomCassetteBlock).IsAssignableFrom(type)) continue;
                foreach (var attribute in type.GetCustomAttributes<CustomEntityAttribute>())
                foreach (string fullId in attribute.IDs)
                    owner.entityNames.Add(fullId.Split('=')[0].Trim());
            }
        }

        private bool OnLoadEntity(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
        {
            if (ReferenceEquals(level.EngineContext, engine) && entityNames.Contains(data.Name))
                PrepareMap(level, data);
            return false;
        }

        private void PrepareMap(OriginalLevelSceneRoot level, EntityData data)
        {
            level.HasCassetteBlocks = true;
            if (level.CassetteBlockTempo == 1f) level.CassetteBlockTempo = data.Float("tempo", 1f);
            level.CassetteBlockBeats = Math.Max(data.Int("index", 0) + 1, level.CassetteBlockBeats);
            if (createdCassetteManager) return;
            createdCassetteManager = true;
            var scene = ((ISourceScene)level).SourceScene;
            if (scene.Entities
                    .GetTrackedEntity<OriginalCassetteBlockManagerState>("Celeste.CassetteBlockManager") is null &&
                ((IInstalledCassetteManagerScene)level).ShouldCreateCassetteManager &&
                !scene.GetToAdd().Any(entity => entity is OriginalCassetteBlockManagerState))
                scene.ForceAddSourceEntity(((IOriginalCassetteManagerEnvironment)level.EngineContext).CassetteManagers
                    .Create());
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (events is not null) events.OnLoadEntity -= OnLoadEntity;
            entityNames.Clear();
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
            Owners.Remove(engine);
        }
    }
}
