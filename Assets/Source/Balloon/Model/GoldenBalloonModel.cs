using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     The golden reward balloon (PLAN-RewardBalloons): plated, so the shot's own ordinary hit bounces off and
    ///     no item touches it. Only a piercing shot pops it, plowing it like an unbreakable and shattering it at
    ///     the next wall; its pop pays a free card (<c>RewardBalloonPayout</c>). Colourless and scoreless, so it
    ///     never counts toward a goal or moves the streak. Not an <see cref="UnbreakableBalloonModel" />: that
    ///     would pull it into formations and roaming.
    /// </summary>
    internal sealed class GoldenBalloonModel : BalloonModelBase, IHasDeflectStamp, IResistsPaint, IDeflectsShots
    {
        public float DeflectStampScale { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }
        public bool DeflectsOrdinaryHit => true;

        internal GoldenBalloonModel(BalloonModelConfig config)
            : base(config)
        {
            DeflectStampScale = config.DeflectStampScale;
            NudgeOverrides = config.NudgeOverrides;
        }

        // An item's damage carries no DirectHit, and a bomb's neighbours and the laser carry Piercing, so the pop
        // needs both: a bomb beside it must not hand out the card. None leaves an item's hit without a bounce.
        public override HitOutcome EvaluateHit(DamageContext context)
        {
            if ((context.Flags & DamageFlags.DirectHit) == 0)
            {
                return HitOutcome.None;
            }

            return (context.Flags & DamageFlags.Piercing) != 0 ? HitOutcome.Pop : HitOutcome.Deflect;
        }
    }
}
