// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Gameplay.Collision;
using Celeste.Physics.Collision;
using Celeste.Mods.CommunalHelper.Content.Stations;
using Celeste.Mods.CommunalHelper.Runtime.Hazards;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using SoundSource = Celeste.Simulation.Audio.Sources.SoundSourceComponentState;
using Player = Celeste.Simulation.RoomPlayerSession;
using DashCollisionResults = Celeste.Simulation.Gameplay.Player.Collision.Surfaces.DashCollisionResult;
using Node = Celeste.Mods.CommunalHelper.Entities.Stations.StationBlockTrack.Node;

namespace Celeste.Mods.CommunalHelper.Entities.Stations
{
    public partial class StationBlock
    {
        private Sprite LookForCustomSprite(string path, string image)
        {
            path += "/";

            float delay;
            switch (image)
            {
                default:
                case "big":
                    delay = 0.06f;
                    break;
                case "medium":
                    image = "med";
                    delay = 0.05f;
                    break;
                case "small":
                    delay = 0.04f;
                    break;
            }

            Sprite sprite = new(Engine, GameAtlas, path);

            sprite.JustifyOrigin(.5f, .5f);

            // <Loop id="IdleUp" path="small" frames="0" delay="0.04"/>
            // <Loop id="IdleRight" path="small" frames="4" delay="0.04"/>
            // <Loop id="IdleDown" path="small" frames="8" delay="0.04"/>
            // <Loop id="IdleLeft" path="small" frames="12" delay="0.04"/>
            sprite.Add("IdleUp", image, delay, 0);
            sprite.Add("IdleRight", image, delay, 4);
            sprite.Add("IdleDown", image, delay, 8);
            sprite.Add("IdleLeft", image, delay, 12);

            // <Anim id="UpToUp" path="small" frames="0,1,0,15,0" delay="0.04" goto="IdleUp"/>
            // <Anim id="RightToRight" path="small" frames="4,3,4,5,4" delay="0.04" goto="IdleRight"/>
            // <Anim id="DownToDown" path="small" frames="8,9,8,7,8" delay="0.04" goto="IdleDown"/>
            // <Anim id="LeftToLeft" path="small" frames="12,13,12,11,12" delay="0.04" goto="IdleLeft"/>
            sprite.Add("UpToUp", image, delay, "IdleUp", 0, 1, 0, 15, 0);
            sprite.Add("RightToRight", image, delay, "IdleRight", 4, 3, 4, 5, 4);
            sprite.Add("DownToDown", image, delay, "IdleDown", 8, 9, 8, 7, 8);
            sprite.Add("LeftToLeft", image, delay, "IdleLeft", 12, 13, 12, 11, 12);

            // <Anim id="UpToRight" path="small" frames="0-4" delay="0.04" goto="IdleRight"/>
            // <Anim id="UpToDown" path="small" frames="0,1,2,6,7,8" delay="0.04" goto="IdleDown"/>
            // <Anim id="UpToLeft" path="small" frames="0,15,14,13,12" delay="0.04" goto="IdleLeft"/>
            sprite.Add("UpToRight", image, delay, "IdleRight", 0, 1, 2, 3, 4);
            sprite.Add("UpToDown", image, delay, "IdleDown", 0, 1, 2, 6, 7, 8);
            sprite.Add("UpToLeft", image, delay, "IdleLeft", 0, 15, 14, 13, 12);

            // <Anim id="RightToUp" path="small" frames="4,3,2,1,0" delay="0.04" goto="IdleUp"/>
            // <Anim id="RightToDown" path="small" frames="4-8" delay="0.04" goto="IdleDown"/>
            // <Anim id="RightToLeft" path="small" frames="4,5,6,10,11,12" delay="0.04" goto="IdleLeft"/>
            sprite.Add("RightToUp", image, delay, "IdleUp", 4, 3, 2, 1, 0);
            sprite.Add("RightToDown", image, delay, "IdleDown", 4, 5, 6, 7, 8);
            sprite.Add("RightToLeft", image, delay, "IdleLeft", 4, 5, 6, 10, 11, 12);

            // <Anim id="DownToRight" path="small" frames="8,7,6,5,4" delay="0.04" goto="IdleRight"/>
            // <Anim id="DownToUp" path="small" frames="8,9,10,14,15,0" delay="0.04" goto="IdleUp"/>
            // <Anim id="DownToLeft" path="small" frames="8-12" delay="0.04" goto="IdleLeft"/>
            sprite.Add("DownToRight", image, delay, "IdleRight", 8, 7, 6, 5, 4);
            sprite.Add("DownToUp", image, delay, "IdleUp", 8, 9, 10, 14, 15, 0);
            sprite.Add("DownToLeft", image, delay, "IdleLeft", 8, 9, 10, 11, 12);

            // <Anim id="LeftToRight" path="small" frames="12,13,14,2,3,4" delay="0.04" goto="IdleRight"/>
            // <Anim id="LeftToUp" path="small" frames="12,13,14,15,0" delay="0.04" goto="IdleUp"/>
            // <Anim id="LeftToDown" path="small" frames="12,11,10,9,8" delay="0.04" goto="IdleDown"/>
            sprite.Add("LeftToRight", image, delay, "IdleRight", 12, 13, 14, 2, 3, 4);
            sprite.Add("LeftToUp", image, delay, "IdleUp", 12, 13, 14, 15, 0);
            sprite.Add("LeftToDown", image, delay, "IdleDown", 12, 11, 10, 9, 8);

            sprite.CenterOrigin();

            return sprite;
        }

        private void GenerateTiles()
        {
            int tileWidth = (int)(Width / 8f);
            int tileHeight = (int)(Height / 8f);
            blockTiles = new MTexture[tileWidth, tileHeight];
            for (int i = 0; i < tileWidth; i++)
            {
                for (int j = 0; j < tileHeight; j++)
                {
                    int x = (i != 0) ? ((i != tileWidth - 1f) ? 1 : 2) : 0;
                    int y = (j != 0) ? ((j != tileHeight - 1f) ? 1 : 2) : 0;
                    blockTiles[i, j] = tileSlices[x, y];
                }
            }
        }
    }
}
