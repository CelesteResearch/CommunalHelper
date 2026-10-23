// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content.Atlas;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.MoveBlock;

namespace Celeste.Mods.CommunalHelper.Entities.Effects
{
    [RuntimeTypeAlias("Celeste.Mod.CommunalHelper.Entities.MoveBlockDebris")]
    public class MoveBlockDebris(
        AtlasData atlas,
        ISourceEngineContext engine,
        IPlatformTargetMotionPolicy targetMotionPolicy)
        : MovingBlockDebrisState(atlas, engine, targetMotionPolicy)
    {
        public Action<SpriteImage> OnUpdateSprite;
        protected override Vector2 ReturnBaseScale => Sprite.Scale;
        protected override PackedColor TransitionBaseColor => Sprite.Color;

        public MoveBlockDebris Init(Vector2 position, Vector2 center, Vector2 returnTo,
            Action<SpriteImage> onUpdateSprite = null)
        {
            OnUpdateSprite = onUpdateSprite;
            base.Init(position, center, returnTo);
            return this;
        }

        public override void Update()
        {
            UpdateActorComponents();
            var bounds = ((OriginalLevelSceneRoot)Scene).Bounds;
            bool isInBounds = Right > bounds.Left && Bottom > bounds.Top && Left < bounds.Right && Top < bounds.Bottom;
            Sprite.Color = PackedColor.White;
            Sprite.Scale = Vector2.One;
            Sprite.Visible = isInBounds;
            OnUpdateSprite?.Invoke(Sprite);
            if (!Returning)
            {
                if (Collidable && isInBounds) AdvanceFalling();
                AdvanceShaking();
            }
            else AdvanceReturn();

            AdvanceVisualState();
        }
    }
}
