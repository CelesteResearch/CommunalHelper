// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;

namespace Celeste.Mods.CommunalHelper.Entities
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.PlayerBubbleRegion")]
    [CustomEntity("CommunalHelper/PlayerBubbleRegion")]
    public sealed class PlayerBubbleRegion : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly Vector2 control;
        private readonly Vector2 end;

        public PlayerBubbleRegion(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, data.Position + offset, data.Width, data.Height, data.NodesOffset(offset))
        {
        }

        public PlayerBubbleRegion(ISourceEngineContext engine, Vector2 position, int width, int height, Vector2[] nodes)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            this.engine = engine;
            control = nodes[0];
            end = nodes[1];
            Depth = -11001;
            Collider = new HitboxColliderGeometry(width, height);
            Add(new PlayerColliderComponentState(OnPlayer));
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            if (player.Dead || player.Machine.State == 21) return;
            var level = (OriginalLevelSceneRoot)Scene;
            ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play("event:/game/general/cassette_bubblereturn",
                level.Camera.Position + new Vector2(160f, 90f));
            player.StartCassetteFly(end, control);
        }
    }
}
