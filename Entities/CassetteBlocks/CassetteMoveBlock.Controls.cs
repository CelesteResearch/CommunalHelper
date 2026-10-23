// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Extensions.Entities.Registry;
using Celeste.Everest.Reflection;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Graphics.Animation;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    public partial class CassetteMoveBlock
    {
        private readonly string playerTrackerKey, actorTrackerKey;

        private RoomPlayerSession CurrentPlayer =>
            ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntity<RoomPlayerSession>(playerTrackerKey);

        private enum ControllerEntry
        {
            Normal,
            AfterMovementYield,
            AfterBreakShake
        }

        private bool OnInterval(float interval) => ((ISourceScene)Scene).SourceScene.Clock.OnInterval(interval, Engine);

        private float Approach(float value, float target, float amount) => ScalarMath.Approach(value, target, amount,
            ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic);

        private float Clamp(float value, float min, float max) =>
            ScalarMath.Clamp(value, min, max, ((ISourceExtremaEnvironment)Engine).ExtremaArithmetic);

        private static MoveBlockDirection EntityEnum(EntityData data) =>
            EntityAttributeReader.Enum(data.Values, "direction", MoveBlockDirection.Left);

        private static float DirectionAngle(MoveBlockDirection direction) => direction switch
        {
            MoveBlockDirection.Right => 0f, MoveBlockDirection.Down => (float)Math.PI / 2f,
            MoveBlockDirection.Left => (float)Math.PI, MoveBlockDirection.Up => -(float)Math.PI / 2f, _ => 0f
        };

        private static Type[] ResolveIgnoredTypes(ISourceEngineContext engine, string names)
        {
            var entries = names.Split(',').Select(s => s.Trim()).Where(s => s.Length != 0).ToArray();
            if (entries.Length == 0) return [];
            var registry = ((IEverestEntityRegistryEnvironment)engine).EntityRegistry;
            var aggregate = ((ISourceEverestReflectionEnvironment)engine).Reflection.GetFakeEntryAssembly();
            var found = entries.SelectMany(registry.GetKnownTypesFromSid).ToList();
            found.AddRange(aggregate.GetTypes()
                .Where(type => entries.Any(entry => aggregate.NameMatches(type, entry))));
            return found.Distinct().ToArray();
        }

        bool IRedirectableMoveBlock.CanSteer => false;

        // Published source captures the constructor parameter, not the readonly movement-speed field.
        float IRedirectableMoveBlock.Speed
        {
            get => redirectMoveSpeed;
            set => redirectMoveSpeed = value;
        }

        float IRedirectableMoveBlock.TargetSpeed
        {
            get => targetSpeed;
            set => targetSpeed = value;
        }

        MoveBlockDirection IRedirectableMoveBlock.Direction
        {
            get => Direction;
            set
            {
                int index = (int)Math.Floor(((0f - angle + (float)Math.PI * 2f) % ((float)Math.PI * 2f) /
                    ((float)Math.PI * 2f) * 8f) + .5f);
                arrow = new SpriteImage(GameAtlas.GetAtlasSubtextures(
                    SuffixFromSpritePathOrDefault("/arrow", "objects/CommunalHelper/cassetteMoveBlock/arrow"))[index]);
                arrowPressed = new SpriteImage(GameAtlas.GetAtlasSubtextures(SuffixFromSpritePathOrDefault(
                    "/arrowPressed",
                    "objects/CommunalHelper/cassetteMoveBlock/arrowPressed"))[index]);
                arrowHighlight = new SpriteImage(GameAtlas.GetAtlasSubtextures(
                    SuffixFromSpritePathOrDefault("/arrowHighlight",
                        "objects/CommunalHelper/cassetteMoveBlock/arrowHighlight"))[index]);
                arrowHighlightPressed = new SpriteImage(GameAtlas.GetAtlasSubtextures(
                    SuffixFromSpritePathOrDefault("/arrowHighlightPressed",
                        "objects/CommunalHelper/cassetteMoveBlock/arrowHighlightPressed"))[index]);
                Direction = value;
            }
        }

        float IRedirectableMoveBlock.Angle
        {
            get => angle;
            set => angle = value;
        }

        float IRedirectableMoveBlock.HomeAngle
        {
            get => homeAngle;
            set => homeAngle = value;
        }

        float IRedirectableMoveBlock.TargetAngle
        {
            get => targetAngle;
            set => targetAngle = value;
        }

        SoundSourceComponentState IRedirectableMoveBlock.MoveSfx => moveSfx;

        public void ResumeController(SimulationCoroutine coroutine) =>
            coroutine.Replace(Controller(ControllerEntry.AfterMovementYield));

        public void BreakController(SimulationCoroutine coroutine)
        {
            groupable.State = GroupableMoveBlock.MovementState.Breaking;
            coroutine.Replace(Controller(ControllerEntry.AfterBreakShake));
        }
    }
}
