using Celeste.Physics.Collision.Riding;

namespace Celeste.Mods.CommunalHelper.Entities.ConnectedBlocks
{
    public partial class ConnectedSolid
    {
        protected override void PushActorH(ISourcePlatformActor actor, int move, float left, float right)
        {
            foreach (var hitbox in Colliders)
            {
                if (!hitbox.Collide(actor.Collider)) continue;
                float regionLeft = X + hitbox.Left;
                float regionRight = X + hitbox.Right;
                int amount = move <= 0 ? (int)(regionLeft - actor.Right) : (int)(regionRight - actor.Left);
                Collidable = false;
                actor.MoveHExact(amount, actor.SquishCallback, this);
                actor.LiftSpeed = LiftSpeed;
                Collidable = true;
            }
        }

        protected override void PushActorV(ISourcePlatformActor actor, int move, float top, float bottom)
        {
            foreach (var hitbox in Colliders)
            {
                if (!hitbox.Collide(actor.Collider)) continue;
                float regionTop = Y + hitbox.Top;
                float regionBottom = Y + hitbox.Bottom;
                int amount = move <= 0 ? (int)(regionTop - actor.Bottom) : (int)(regionBottom - actor.Top);
                Collidable = false;
                actor.MoveVExact(amount, actor.SquishCallback, this);
                actor.LiftSpeed = LiftSpeed;
                Collidable = true;
            }
        }
    }
}
