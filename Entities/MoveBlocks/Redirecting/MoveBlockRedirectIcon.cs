// Copyright (c) 2020 Flynn Swainston-Calcutt. Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Audio;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Directions = Celeste.Simulation.Gameplay.Platforms.MoveBlock.MoveBlockDirection;
using Color = Celeste.Core.Display.PackedColor;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;

namespace Celeste.Mods.CommunalHelper.Entities.MoveBlocks.Redirecting
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.MoveBlockRedirect+Icon")]
    internal sealed class MoveBlockRedirectIcon : SceneEntityState
    {
        public Image Sprite;

        public MoveBlockRedirectIcon(ISourceEngineContext engine, Vector2 position, float rotation, string iconName,
            string reskinFolder)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            Depth = 2000;

            MTexture defaultTexture =
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[
                    "objects/CommunalHelper/moveBlockRedirect/" + iconName];
            MTexture icon;
            if (reskinFolder is null)
            {
                icon = defaultTexture;
            }
            else
            {
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas.PushFallback(defaultTexture);
                icon = ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[$"{reskinFolder}/{iconName}"];
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas.PopFallback();
            }

            OwnedComponents.Add(Sprite = new Image(icon));

            Sprite.CenterOrigin();
            Sprite.Rotation = rotation;
        }
    }
}
