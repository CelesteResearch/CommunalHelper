// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Rendering;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Celeste.Simulation.Gameplay.Enemies.Seeker;
using Celeste.Mods.CommunalHelper.Components.Redirecting;
using Celeste.Everest.Modules.Runtime;
using Celeste.Mods.CommunalHelper.Modules;
using Celeste.Mods.CommunalHelper.Content;
using Celeste.Mods.CommunalHelper.Components.MoveBlocks;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Image = Celeste.Graphics.Animation.SpriteImage;
using MTexture = Celeste.Graphics.Content.Atlas.AtlasEntry;
using Color = Celeste.Core.Display.PackedColor;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    [CustomEntity("CommunalHelper/ConnectedMoveBlock")]
    [Celeste.Scene.Tracking.Attributes.SceneTracked]
    public partial class ConnectedMoveBlock : ConnectedSolid, IRedirectableMoveBlock
    {
        // Custom Border Entity


        protected GroupableMoveBlock groupable;

        protected readonly MTexture[,] masterEdges;
        protected readonly MTexture[,] masterInnerCorners;
        protected readonly List<MTexture> masterArrows;
        protected readonly List<MTexture> masterDebris;
        private readonly ConnectedMoveBlockTextures textureBank;
        protected MTexture masterX => textureBank.BreakingIcon;

        //Custom Texture support
        protected bool customTexture;
        protected Tuple<MTexture[,], MTexture[,]> tiles;
        protected List<MTexture> arrows;
        protected MTexture x;
        protected List<MTexture> debris;

        protected readonly bool noArrowSprite, noBreakingSprite;

        //Custom Sound support
        protected string ActivateSoundEffect = "event:/game/04_cliffside/arrowblock_activate";
        protected string MoveSoundEffect = "event:/CommunalHelperEvents/game/redirectMoveBlock/arrowblock_move";
        protected string BreakSoundEffect = "event:/game/04_cliffside/arrowblock_break";
        protected string ReformBeginSoundEffect = "event:/game/04_cliffside/arrowblock_reform_begin";
        protected string ReappearSoundEffect = "event:/game/04_cliffside/arrowblock_reappear";

        //ColorModifiers added to entityData constructor.
        protected readonly Color idleBgFill = ParseColor("474070");
        protected readonly Color pressedBgFill = ParseColor("30b335");
        protected readonly Color breakingBgFill = ParseColor("cc2541");
        protected Color fillColor;

        protected float particleRemainder;

        protected Vector2 startPosition;

        public MoveBlockDirection Direction;

        protected List<Hitbox> ArrowsList;

        protected const float CrashResetTime = 0.1f;
        protected const float CrashStartShakingTime = 0.15f;
        protected readonly float crashTime;
        protected readonly float regenTime;
        protected readonly bool shakeOnCollision;

        protected float moveSpeed;
        protected bool triggered;

        protected float speed;
        protected float targetSpeed;

        protected float angle;
        protected float targetAngle;
        protected float homeAngle;

        protected float flash;
        protected Border border;

        protected RoomPlayerSession noSquish;

        protected SoundSourceComponentState moveSfx;

        // Flag options
        // A list of sets of flags. When all in a set are on (or off, and preceded by "!"), the Connected Move Block will activate.
        // The flag "_pressed" is considered to be on when the player is riding the block. This is the only set by default.
        // Sets are separated by "|" and flags in a set are separated by ",".
        protected List<List<string>> ActivatorFlags = new();

        // A list of flags to be enabled (or disabled when preceded by "!", or toggled when preceded by "~") when the Connected Move Block activates.
        protected List<string> OnActivateFlags = new();

        // A list of sets of flags. When all are on (or off, and preceded by !), the Connected Move Block will immediately break.
        // The flag "_obstructed" is considered to be on when the block is obstructed (and not broken) by a solid or screen edge. This is the only set by default.
        // Sets are separated by '|' and flags in a set are separated by ','.
        protected List<List<string>> BreakerFlags = new();

        // A list of flags to be enabled (or disabled when preceded by "!", or toggled when preceded by "~") when the Connected Move Block breaks.
        protected List<string> OnBreakFlags = new();

        // If true, OnBreakFlags will not be set if the block breaks inside a seeker barrier.
        protected bool BarrierBlocksFlags = false;

        // If true, the block will not appear until breaker flags are disabled.
        protected bool WaitForFlags = false;

        protected bool curMoveCheck = false;

        private readonly bool outline;

        protected readonly bool noDebris;

        // whether any MoveBlockRedirects' effects on this move block should persist after respawn
        protected readonly bool redirectIsPersistent;

        // when moving, ignore these solid types
        protected readonly Type[] ignores;

        public static ConnectedMoveBlock Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data)
            => new(data, offset, level.SolidWorldFactory);

        public ConnectedMoveBlock(EntityData data, Vector2 offset, ISourceSolidWorldFactory worlds)
            : this(data.Position + offset, data.Width, data.Height, data.Enum<MoveBlockDirection>("direction"),
                data.Bool("fast") ? 75f : data.Float("moveSpeed", 60f), worlds.Create(data.Position + offset, false))
        {
            idleBgFill = ParseColor(data.Attr("idleColor", "474070"));
            pressedBgFill = ParseColor(data.Attr("pressedColor", "30b335"));
            breakingBgFill = ParseColor(data.Attr("breakColor", "cc2541"));
            fillColor = idleBgFill;

            string customSkin = data.Attr("customSkin").Trim().TrimEnd('/');
            string legacyCustomTexture = data.Attr("customBlockTexture").Trim().TrimEnd('/');
            if (!string.IsNullOrEmpty(customSkin))
            {
                // non-legacy
                tiles = SetupCustomTileset(SourceWorld.EngineContext, customSkin + "/tileset", false);
                arrows = GameAtlas.GetAtlasSubtextures(customSkin + "/arrow");
                if (arrows.Count < 8)
                    arrows = null;
                x = GameAtlas[customSkin + "/x"];
                debris = GameAtlas.GetAtlasSubtextures(customSkin + "/debris");
                if (debris.Count < 1)
                    debris = null;

                customTexture = true;
            }
            else if (!string.IsNullOrEmpty(legacyCustomTexture))
            {
                // legacy
                // im gonna crash out
                string tilesetPath;
                if (!GameAtlas.Has("objects/" + legacyCustomTexture))
                {
                    tilesetPath = legacyCustomTexture + "/tileset";
                    arrows = GameAtlas.GetAtlasSubtextures("objects/" + legacyCustomTexture + "/arrow");
                    if (arrows.Count < 8)
                        arrows = null;
                    x = GameAtlas["objects/" + legacyCustomTexture + "/x"];
                    debris = GameAtlas.GetAtlasSubtextures("objects/" + legacyCustomTexture + "/debris");
                    if (debris.Count < 1)
                        debris = null;
                }
                else
                {
                    List<string> containingFolders = new();
                    containingFolders.AddRange(legacyCustomTexture.Split('/'));
                    containingFolders.RemoveAt(containingFolders.Count - 1);
                    string containingFolder = string.Join("/", containingFolders);

                    tilesetPath = legacyCustomTexture;
                    arrows = GameAtlas.GetAtlasSubtextures("objects/" + containingFolder + "/arrow");
                    if (arrows.Count < 8)
                        arrows = null;
                    x = GameAtlas["objects/" + containingFolder + "/x"];
                    debris = GameAtlas.GetAtlasSubtextures("objects/" + containingFolder + "/debris");
                    debris = null;
                }

                tiles = SetupCustomTileset(SourceWorld.EngineContext, tilesetPath, true);
                customTexture = true;
            }

            LoadCustomSounds(data.Attr("customSoundEffect"));

            noArrowSprite = data.Bool("noArrowSprite", false);
            noBreakingSprite = data.Bool("noBreakingSprite", false);
            noDebris = data.Bool("noDebris", false);

            outline = data.Bool("outline", true);

            ActivatorFlags.AddRange(data.Attr("activatorFlags", "_pressed").Split('|')
                .Select(l => l.Split(',').ToList()));
            BreakerFlags.AddRange(
                data.Attr("breakerFlags", "_obstructed").Split('|').Select(l => l.Split(',').ToList()));
            OnActivateFlags.AddRange(data.Attr("onActivateFlags", "").Split(','));
            OnBreakFlags.AddRange(data.Attr("onBreakFlags", "").Split(','));
            BarrierBlocksFlags = data.Bool("barrierBlocksFlags", false);
            WaitForFlags = data.Bool("waitForFlags", false);

            crashTime = data.Float("crashTime", 0.15f);
            regenTime = data.Float("regenTime", 3f);
            shakeOnCollision = data.Bool("shakeOnCollision", true);

            redirectIsPersistent = data.Bool("redirectIsPersistent", true);

            ignores = ResolveIgnoredTypes(data.Attr("ignore"));
        }

        public ConnectedMoveBlock(Vector2 position, int width, int height, MoveBlockDirection direction,
            float moveSpeed,
            ISourceSolidWorld world)
            : base(position, width, height, false, world)
        {
            textureBank = ((ISourceEverestModulesEnvironment)SourceWorld.EngineContext).Modules
                          .OfType<CommunalHelperModule>().Single().Textures
                          ?? throw new InvalidOperationException(
                              "Connected block textures must be loaded before constructing map entities.");
            masterEdges = textureBank.Edges;
            masterInnerCorners = textureBank.InnerCorners;
            masterArrows = textureBank.Arrows;
            masterDebris = textureBank.Debris;
            Depth = -1;
            startPosition = position;
            Direction = direction;
            this.moveSpeed = moveSpeed;

            homeAngle = targetAngle = angle = DirectionAngle(direction);
            Add(moveSfx = ((ISourceSoundSourceEnvironment)SourceWorld.EngineContext).SoundSources.Create());
            Add(new SimulationCoroutine(SourceWorld.EngineContext, Controller()));
            Add(groupable = new GroupableMoveBlock());
            UpdateColors();
        }

        public override void OnStaticMoverTrigger(SourceStaticMoverState sm)
        {
            base.OnStaticMoverTrigger(sm);

            // If this move block has the "_pressed" flag, meaning it can be manually activated by the player,
            // then it will be activated by a static mover being triggered.
            if (ActivatorFlags.Any(set => set.Any(flag => flag == "_pressed")))
                triggered = true;
        }


        protected void LoadCustomSounds(string customSoundEffectPath)
        {
            void LoadSfxIfPresent(string sfxPath, ref string target)
            {
                if (((IOriginalGameplayProcess)SourceWorld.EngineContext).AudioSession.Runtime
                    .GetSourceEventDescription(sfxPath) is not null)
                {
                    target = sfxPath;
                }
            }

            customSoundEffectPath = customSoundEffectPath.Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(customSoundEffectPath))
            {
                if (!customSoundEffectPath.StartsWith("event:/"))
                {
                    customSoundEffectPath = customSoundEffectPath.TrimStart('/');
                    customSoundEffectPath = $"event:/{customSoundEffectPath}";
                }

                LoadSfxIfPresent($"{customSoundEffectPath}_activate", ref ActivateSoundEffect);
                LoadSfxIfPresent($"{customSoundEffectPath}_move_loop", ref MoveSoundEffect);
                LoadSfxIfPresent($"{customSoundEffectPath}_break", ref BreakSoundEffect);
                LoadSfxIfPresent($"{customSoundEffectPath}_reform_begin", ref ReformBeginSoundEffect);
                LoadSfxIfPresent($"{customSoundEffectPath}_reappear", ref ReappearSoundEffect);
            }
        }

        protected void UpdateColors()
        {
            Color value = groupable.State switch
            {
                GroupableMoveBlock.MovementState.Moving => pressedBgFill,
                GroupableMoveBlock.MovementState.Breaking => breakingBgFill,
                _ => idleBgFill,
            };
            fillColor = Color.Lerp(fillColor, value, 10f * SourceWorld.EngineContext.DeltaTime);
        }

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

        protected bool MoveCheck(Vector2 speed)
        {
            if (speed.X != 0f)
            {
                if (MoveHCollideSolidsExcluding(speed.X))
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        for (int num = 1; num >= -1; num -= 2)
                        {
                            Vector2 vector = new Vector2(Math.Sign(speed.X), i * num);
                            if (!CollideSolidsExcluding(Position + vector))
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
                if (MoveVCollideSolidsExcluding(speed.Y))
                {
                    for (int j = 1; j <= 3; j++)
                    {
                        for (int num2 = 1; num2 >= -1; num2 -= 2)
                        {
                            Vector2 vector2 = new Vector2(j * num2, Math.Sign(speed.Y));
                            if (!CollideSolidsExcluding(Position + vector2))
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


        protected bool AnySetEnabled(List<List<string>> sets)
        {
            return sets.Any(SetEnabled);
        }

        protected bool SetEnabled(List<string> set)
        {
            return set.All(s => s.Equals("_pressed") ? HasPlayerRider()
                : s.Equals("_obstructed") ? curMoveCheck
                : s.StartsWith("!") ? !Level.Session.GetFlag(s.Substring(1))
                : Level.Session.GetFlag(s));
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (customTexture)
            {
                AutoTile(tiles.Item1, tiles.Item2);
            }
            else
            {
                AutoTile(masterEdges, masterInnerCorners);
            }

            ((ISourceScene)scene).SourceScene.QueueSourceEntity(border = new Border(this));

            // Get all the colliders that can have an arrow drawn on.
            ArrowsList = new List<Hitbox> { (Hitbox)MasterCollider };
            foreach (Hitbox hitbox in Colliders)
            {
                if (Math.Min(hitbox.Width, hitbox.Height) >= 24)
                {
                    ArrowsList.Add(hitbox);
                }
            }

            // Allow this block to be redirected by MoveBlockRedirects if it has a single rectangular collider.
            if (Colliders.Length == 1)
            {
                Add(new MoveBlockRedirectable(this));
            }

            AddLightOccluders(0.5f);
        }

        public override void Update()
        {
            base.Update();
            if (moveSfx is not null && moveSfx.Playing)
            {
                float fullTurn = (float)Math.PI * 2f;
                int num = (int)Math.Floor(
                    ((0f - Vector2Geometry.Angle(Vector2Geometry.AngleToVector(angle, 1f) * new Vector2(-1f, 1f)) +
                      fullTurn) % fullTurn / fullTurn * 8f) + 0.5f);
                moveSfx.Param("arrow_influence", num + 1);
            }

            border.Visible = Visible;
            flash = Approach(flash, 0f, SourceWorld.EngineContext.DeltaTime * 5f);
            UpdateColors();
        }
    }
}
