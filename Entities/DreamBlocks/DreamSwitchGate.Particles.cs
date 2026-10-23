using System.Collections;
using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Components.Animation;
using Celeste.Core.Mathematics;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Modules.Metadata;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Modules.Updating;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Session;
using Celeste.Game.Gameplay.Mechanisms.SwitchGates;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Display.Colors;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Entities;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.CommunalHelper.Runtime.DreamBlocks.Gates;
using Celeste.Game.Components.Switches;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Content;
using Color = Celeste.Core.Display.PackedColor;
using ParticleType = Celeste.Simulation.Presentation.Particles.Definitions.ParticleTypePresentation;
using SoundSource = Celeste.Simulation.Audio.Sources.SoundSourceComponentState;
using Wiggler = Celeste.Scene.Components.Oscillation.WigglerComponent;
using Tween = Celeste.Core.Components.Animation.SourceTweenState;

namespace Celeste.Mods.CommunalHelper.Entities.DreamBlocks
{
    public partial class DreamSwitchGate
    {
        public override void SetupCustomParticles(float canvasWidth, float canvasHeight)
        {
            base.SetupCustomParticles(canvasWidth, canvasHeight);
            if (PlayerHasDreamDash)
            {
                P_BehindDreamParticles[0].Color = SourceHexColor.HexToColor("FFEF11");
                P_BehindDreamParticles[0].Color2 = SourceHexColor.HexToColor("FF00D0");

                P_BehindDreamParticles[1].Color = SourceHexColor.HexToColor("08a310");
                P_BehindDreamParticles[1].Color2 = SourceHexColor.HexToColor("5fcde4");

                P_BehindDreamParticles[2].Color = SourceHexColor.HexToColor("7fb25e");
                P_BehindDreamParticles[2].Color2 = SourceHexColor.HexToColor("E0564C");

                P_BehindDreamParticles[3].Color = SourceHexColor.HexToColor("5b6ee1");
                P_BehindDreamParticles[3].Color2 = SourceHexColor.HexToColor("CC3B3B");
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    P_BehindDreamParticles[i].Color = Color.LightGray * 0.5f;
                    P_BehindDreamParticles[i].Color2 = Color.LightGray * 0.75f;
                }
            }
        }
    }
}
