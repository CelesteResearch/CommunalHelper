// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedMoveBlock
    {
        private AtlasData GameAtlas =>
            ((ISourceGameGraphicsEnvironment)SourceWorld.EngineContext).GameGraphics.GameAtlas;

        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        private ISourceSpatialAudio SpatialAudio =>
            ((ISourceSpatialAudioEnvironment)SourceWorld.EngineContext).SpatialAudio;

        private RoomPlayerSession FirstPlayer =>
            ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>(typeof(RoomPlayerSession)
                .FullName);

        private bool OnInterval(float interval) =>
            ((ISourceScene)Scene).SourceScene.Clock.OnInterval(interval, SourceWorld.EngineContext);

        private float Approach(float value, float target, float amount) => ScalarMath.Approach(value, target, amount,
            ((ISourceExtremaEnvironment)SourceWorld.EngineContext).ExtremaArithmetic);

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private static PackedColor ParseColor(string value)
        {
            var named = typeof(PackedColor).GetProperty(value,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            PackedColor result = named?.PropertyType == typeof(PackedColor)
                ? (PackedColor)named.GetValue(null)
                : SourceHexColor.HexToColor(value.Trim('#'));
            result.A = 255;
            return result;
        }

        private static float DirectionAngle(MoveBlockDirection direction) => direction switch
        {
            MoveBlockDirection.Left => (float)Math.PI,
            MoveBlockDirection.Up => -(float)Math.PI / 2,
            MoveBlockDirection.Down => (float)Math.PI / 2,
            _ => 0
        };
    }
}
