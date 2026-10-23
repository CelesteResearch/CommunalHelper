// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Celeste.Core.Components;
using Celeste.Core.Rendering;
using Celeste.Graphics.Animation;
using Celeste.Scene.Entities;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public sealed class BGTilesRenderer : SceneEntityState
    {
        private readonly ConnectedSolid solid;
        public List<SpriteImage> BGTiles = new();

        public BGTilesRenderer(ConnectedSolid solid, ISourceEntitySourceDataRegistration sourceData) : base(sourceData)
        {
            this.solid = solid;
            Depth = Math.Max(0, solid.Depth) + 1;
        }

        public override void Update()
        {
            Visible = solid.Visible;
            base.Update();
        }

        public override void Render(ISourceRenderContext context)
        {
            Position = solid.Position + solid.Shake;
            base.Render(context);
        }
    }
}
