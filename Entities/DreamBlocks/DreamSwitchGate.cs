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
/*
 * Lots of stuff taken from Maddie's Helping Hand Flag Switch Gate entity
 * https://github.com/max4805/MaxHelpingHand/blob/master/Entities/FlagSwitchGate.cs
 */

    [CustomEntity("CommunalHelper/DreamSwitchGate = Load",
        "CommunalHelper/MaxHelpingHand/DreamFlagSwitchGate = DreamFlagSwitchGate")]
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.DreamSwitchGate")]
    public partial class DreamSwitchGate : CustomDreamBlock
    {
        private readonly DreamGateParticleOwner particleOwner;
        private ParticleType[] P_BehindDreamParticles => particleOwner.Types;
        private readonly IOriginalSwitchGateProcess process;
        private OriginalLevelSceneRoot Level => (OriginalLevelSceneRoot)Scene;

        private readonly ParticleType P_RecoloredFire;
        private readonly ParticleType P_RecoloredFireBack;

        private readonly bool permanent;

        private readonly Sprite icon;
        private Vector2 iconOffset;
        private readonly Wiggler wiggler;

        private new Vector2 node;

        private readonly SoundSource openSfx;

        public int ID { get; private set; }
        public string Flag { get; private set; }

        public bool Triggered { get; private set; }

        private Color inactiveColor = SourceHexColor.HexToColor("5fcde4");
        private Color activeColor = Color.White;
        private Color finishColor = SourceHexColor.HexToColor("f141df");

        private readonly float shakeTime;
        private readonly float moveTime;
        private readonly bool moveEased;

        private readonly string moveSound;
        private readonly string finishedSound;

        private readonly bool allowReturn;

        private bool isFlagSwitchGate;

        public static DreamSwitchGate DreamFlagSwitchGate(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData,
            Vector2 offset, OriginalLevelEntityData entityData)
        {
            entityData.Values["permanent"] = entityData.Bool("persistent");
            return new DreamSwitchGate(level.EngineContext, entityData, offset, level.SolidWorldFactory)
                { isFlagSwitchGate = true };
        }

        public static DreamSwitchGate Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(level.EngineContext, data, offset, level.SolidWorldFactory);

        public DreamSwitchGate(ISourceEngineContext engine, EntityData data, Vector2 offset,
            ISourceSolidWorldFactory worlds)
            : base(engine, data, offset, worlds)
        {
            process = (IOriginalSwitchGateProcess)engine;
            particleOwner = DreamGateParticleOwner.For(engine);
            permanent = data.Bool("permanent");
            node = data.Nodes[0] + offset;

            isFlagSwitchGate = data.Bool("isFlagSwitchGate");

            ID = data.ID;
            Flag = data.Attr("flag");

            inactiveColor = SourceHexColor.HexToColor(data.Attr("inactiveColor", "5FCDE4"));
            activeColor = SourceHexColor.HexToColor(data.Attr("activeColor", "FFFFFF"));
            finishColor = SourceHexColor.HexToColor(data.Attr("finishColor", "F141DF"));

            shakeTime = data.Float("shakeTime", 0.5f);
            moveTime = data.Float("moveTime", 1.8f);
            moveEased = data.Bool("moveEased", true);

            moveSound = data.Attr("moveSound", "event:/game/general/touchswitch_gate_open");
            finishedSound = data.Attr("finishedSound", "event:/game/general/touchswitch_gate_finish");

            allowReturn = data.Bool("allowReturn");

            P_RecoloredFire = new ParticleType(process.ParticleTypes.TouchSwitch.P_Fire)
            {
                Color = finishColor
            };
            P_RecoloredFireBack = new ParticleType(process.ParticleTypes.TouchSwitch.P_Fire)
            {
                Color = inactiveColor
            };

            string iconAttribute = data.Attr("icon", "vanilla");
            icon = new Sprite(process, process.GameGraphics.GameAtlas,
                iconAttribute == "vanilla"
                    ? "objects/switchgate/icon"
                    : $"objects/MaxHelpingHand/flagSwitchGate/{iconAttribute}/icon");
            icon.Add("spin", "", 0.1f, new Chooser<string>("spin"));
            icon.Play("spin");
            icon.Rate = 0f;
            icon.Color = inactiveColor;
            icon.CenterOrigin();
            iconOffset = new Vector2(Width, Height) / 2f;
            OwnedComponents.Add(wiggler =
                process.WigglerPool.Create(0.5f, 4f, scale => { icon.Scale = Vector2.One * (1f + scale); }));

            OwnedComponents.Add(openSfx = process.SoundSources.Create());
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);

            bool check = isFlagSwitchGate
                ? (Level.Session.GetFlag(Flag + "_gate" + ID) && !allowReturn) || Level.Session.GetFlag(Flag)
                : OriginalSwitchComponent.CheckLevelFlag(Level);

            if (check)
            {
                if (allowReturn && isFlagSwitchGate)
                {
                    OwnedComponents.Add(new SimulationCoroutine(Engine,
                        MoveBackAndForthSequence(Position, node, startAtNode: true)));
                }

                MoveTo(node);
                icon.Rate = 0f;
                icon.SetAnimationFrame(0);
                icon.Color = finishColor;
            }
            else
            {
                if (isFlagSwitchGate)
                {
                    if (allowReturn)
                    {
                        // go back and forth as needed.
                        OwnedComponents.Add(new SimulationCoroutine(Engine,
                            MoveBackAndForthSequence(Position, node, startAtNode: false)));
                    }
                    else
                    {
                        // we are only going to the node, then stopping.
                        OwnedComponents.Add(new SimulationCoroutine(Engine,
                            MaxHelpingHandSequence(node, goingBack: false)));
                    }
                }
                else
                {
                    OwnedComponents.Add(new SimulationCoroutine(Engine, CommunalHelperSequence(node)));
                }
            }
        }
    }
}
