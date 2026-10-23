using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Scene.Tracking;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Mods.CommunalHelper.Entities.CassetteBlocks;
using Celeste.Mods.CommunalHelper.Persistence;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.CommunalHelper.Runtime.CassetteBlocks
{
    internal sealed class CassetteJumpFixProcess : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, CassetteJumpFixProcess> Owners = new();
        private static readonly Action<CassetteBlockState, int> SubtractHeight = BuildHeightAdjustment();
        private readonly ISourceEngineContext engine;
        private readonly Func<CommunalHelperSession> getSession;
        private readonly List<IDisposable> hooks = new();
        private bool disposed;
        internal readonly string ControllerKey;
        internal CommunalHelperSession Session => getSession();

        internal static CassetteJumpFixProcess For(ISourceEngineContext engine) => Owners.GetValue(engine,
            _ => throw new InvalidOperationException("The cassette jump runtime is not loaded."));

        private CassetteJumpFixProcess(ISourceEngineContext engine, Func<CommunalHelperSession> getSession)
        {
            this.engine = engine;
            this.getSession = getSession;
            ControllerKey = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog
                .Find(typeof(CassetteJumpFixController))
                .RegistryKey;
        }

        internal static IDisposable Install(ISourceEngineContext engine, Func<CommunalHelperSession> getSession)
        {
            var process = new CassetteJumpFixProcess(engine, getSession);
            Owners.Add(engine, process);
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                process.hooks.Add(new Hook(typeof(CassetteBlockState).GetMethod("ShiftSize", flags),
                    (Action<Action<CassetteBlockState, int>, CassetteBlockState, int>)((orig, block, amount) =>
                    {
                        if (process.ShouldApply(block)) Apply(block, amount);
                        else orig(block, amount);
                    })));
                // Original JumpFix was installed after CustomCassetteBlock's ShiftSize hook;
                // it bypasses that hook completely. The true overriding port entry must have the same priority.
                process.hooks.Add(new Hook(typeof(CustomCassetteBlock).GetMethod("ShiftSize", flags),
                    (Action<Action<CustomCassetteBlock, int>, CustomCassetteBlock, int>)((orig, block, amount) =>
                    {
                        if (process.ShouldApply(block)) Apply(block, amount);
                        else orig(block, amount);
                    })));
                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        private bool ShouldApply(CassetteBlockState block) => block.Scene is ISourceEngineContextScene scene &&
                                                              ReferenceEquals(scene.EngineContext, engine) &&
                                                              CassetteJumpFixController.MustApply(block.Scene);

        private static void Apply(CassetteBlockState block, int amount)
        {
            block.MoveV(amount, 0f);
            SubtractHeight(block, amount);
        }

        private static Action<CassetteBlockState, int> BuildHeightAdjustment()
        {
            var field = typeof(CassetteBlockState).GetField("blockHeight",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var method = new DynamicMethod("SubtractCassetteHeight", typeof(void),
                new[] { typeof(CassetteBlockState), typeof(int) }, typeof(CassetteJumpFixProcess), true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            return (Action<CassetteBlockState, int>)method.CreateDelegate(typeof(Action<CassetteBlockState, int>));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
            Owners.Remove(engine);
        }
    }
}
