// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Maps;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Framework;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Entities.Platforms.SwapBlocks.Motion;
using Celeste.Game.Entities.Platforms.SwapBlocks.Rendering;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.CommunalHelper.Presentation.Colors;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Dash;
using Color = Celeste.Core.Display.PackedColor;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Celeste.Simulation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Graphics.Rendering;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Celeste.Mods.CommunalHelper.Physics;
using Directions = Celeste.Simulation.Gameplay.Platforms.MoveBlock.MoveBlockDirection;
using MoveBlockDebris = Celeste.Mods.CommunalHelper.Entities.Effects.MoveBlockDebris;

namespace Celeste.Mods.CommunalHelper.Entities.CassetteBlocks
{
    public partial class CassetteMoveBlock
    {
        public override void MoveHExact(int move)
        {
            if (noSquish is not null && ((move < 0 && noSquish.X < X) || (move > 0 && noSquish.X > X)))
            {
                while (move != 0 && noSquish.CollideCheck("Celeste.Solid", noSquish.Position + (Vector2.UnitX * move)))
                {
                    move -= Math.Sign(move);
                }
            }

            base.MoveHExact(move);
        }

        public override void MoveVExact(int move)
        {
            if (noSquish is not null && move < 0 && noSquish.Y <= Y)
            {
                while (move != 0 && noSquish.CollideCheck("Celeste.Solid", noSquish.Position + (Vector2.UnitY * move)))
                {
                    move -= Math.Sign(move);
                }
            }

            base.MoveVExact(move);
        }

        private bool MoveCheck(Vector2 speed)
        {
            if (speed.X != 0f)
            {
                if (SolidCollisionExclusions.MoveHorizontal(this, Engine, ignores, speed.X))
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        for (int num = 1; num >= -1; num -= 2)
                        {
                            Vector2 vector = new Vector2(Math.Sign(speed.X), i * num);
                            if (!SolidCollisionExclusions.Check(this, ignores, Position + vector))
                            {
                                MoveVExact(i * num);
                                MoveHExact(Math.Sign(speed.X));
                                return false;
                            }
                        }
                    }

                    return true;
                }

                return false;
            }

            if (speed.Y != 0f)
            {
                if (SolidCollisionExclusions.MoveVertical(this, Engine, ignores, speed.Y))
                {
                    for (int j = 1; j <= 3; j++)
                    {
                        for (int num2 = 1; num2 >= -1; num2 -= 2)
                        {
                            Vector2 vector2 = new Vector2(j * num2, Math.Sign(speed.Y));
                            if (!SolidCollisionExclusions.Check(this, ignores, Position + vector2))
                            {
                                MoveHExact(j * num2);
                                MoveVExact(Math.Sign(speed.Y));
                                return false;
                            }
                        }
                    }

                    return true;
                }

                return false;
            }

            return false;
        }
    }
}
