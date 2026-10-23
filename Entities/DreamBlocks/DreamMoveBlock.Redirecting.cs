// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celeste.Graphics.Rendering.Drawing;
using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Entities.Platforms.ZipMovers.Motion;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Platforms.ZipMovers;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;
using System.Runtime.CompilerServices;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Mathematics.Easing;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Mods.CommunalHelper.Entities.Effects;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Core.Randomness;
using Player = Celeste.Simulation.RoomPlayerSession;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using StaticMover = Celeste.Simulation.Gameplay.Platforms.StaticMovers.SourceStaticMoverState;
using EventInstance = Celeste.Core.Audio.ISourceAudioEventReference;
using Coroutine = Celeste.Core.Scheduling.SimulationCoroutine;
using LightOcclude = Celeste.Simulation.Presentation.Lighting.LightOccludeState;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    public partial class DreamMoveBlock
    {
        private enum ControllerEntry
        {
            Normal,
            AfterMovementYield,
            AfterBreakShake
        }

        bool IRedirectableMoveBlock.CanSteer => false;

        float IRedirectableMoveBlock.Speed
        {
            get => speed;
            set => speed = value;
        }

        float IRedirectableMoveBlock.TargetSpeed
        {
            get => targetSpeed;
            set => targetSpeed = value;
        }

        MoveBlockDirection IRedirectableMoveBlock.Direction
        {
            get => direction;
            set => direction = value;
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

        // These entries replace original iterator state 4 and 5 without binding to compiler state numbers.
        public void ResumeController(SimulationCoroutine coroutine)
            => coroutine.Replace(Controller(ControllerEntry.AfterMovementYield));

        public void BreakController(SimulationCoroutine coroutine)
        {
            groupable.State = GroupableMoveBlock.MovementState.Breaking;
            coroutine.Replace(Controller(ControllerEntry.AfterBreakShake));
        }
    }
}
