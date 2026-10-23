using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Simulation;

namespace Celeste.Mods.CommunalHelper.Entities.Hazards.TriggerSpikes
{
    public partial class TimedTriggerSpikes
    {
        protected struct SpikeInfo
        {
            public TimedTriggerSpikes Parent;

            public int Index;

            public int TextureIndex;

            public Vector2 Position;

            private bool triggered;

            public bool Triggered
            {
                get => Parent.grouped ? Parent.Triggered : triggered;
                set
                {
                    if (Parent.grouped)
                        Parent.Triggered = value;
                    else
                        triggered = value;
                }
            }

            public float RetractTimer;
            public float DelayTimer;

            public float Lerp;

            public PackedColor color;
            public float ReusableTimer;

            public void Update()
            {
                if (Triggered)
                {
                    if (DelayTimer > 0f)
                    {
                        DelayTimer -= Parent.engine.DeltaTime;
                        if (DelayTimer <= 0f)
                        {
                            var check = PlayerCheck();
                            if (check || (Parent.grouped && Parent.waitForPlayer && Parent.playerPresent))
                            {
                                DelayTimer = 0.05f;
                            }
                            else
                            {
                                Parent.PlaySound("event:/game/03_resort/fluff_tendril_emerge",
                                    Parent.Position + Position);
                            }
                        }
                    }
                    else
                    {
                        Lerp = ScalarMath.Approach(Lerp, 1f, 8f * Parent.engine.DeltaTime);
                        if (Lerp >= 1f && Parent.reusable)
                        {
                            ReusableTimer -= Parent.engine.DeltaTime;
                            if (ReusableTimer <= 0f)
                            {
                                Triggered = false;
                                if (Parent.grouped)
                                {
                                    SpikeInfo[] spikes = Parent.spikes;
                                    int length = spikes.Length;
                                    for (int i = 0; i < length; i++)
                                    {
                                        spikes[i].ReusableTimer = Parent.reusableTimer;
                                        spikes[i].DelayTimer = Parent.Delay;
                                    }
                                }
                                else
                                {
                                    ReusableTimer = Parent.reusableTimer;
                                    DelayTimer = Parent.Delay;
                                }
                            }
                        }
                    }
                }
                else
                {
                    Lerp = ScalarMath.Approach(Lerp, 0f, 4f * Parent.engine.DeltaTime);
                    if (Lerp <= 0f)
                    {
                        Triggered = false;
                    }
                }

                if (Parent.rainbow)
                    color = Parent.GetHue(Parent.Scene, Parent.Position + Position);
            }

            public bool PlayerCheck()
            {
                return Parent.PlayerCheck(Index);
            }

            public bool OnPlayer(RoomPlayerSession player, Vector2 outwards)
            {
                if (!Triggered)
                {
                    Parent.PlaySound("event:/game/03_resort/fluff_tendril_touch", Parent.Position + Position);
                    Triggered = true;
                    RetractTimer = RetractTime;
                    return false;
                }

                if (Lerp >= 1f)
                {
                    player.Die(outwards);
                    return true;
                }

                return false;
            }
        }
    }
}
