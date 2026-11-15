using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     A plated reward balloon (the golden balloon, the disco ball): the shot's own ordinary hit bounces off it,
    ///     and no item but the laser touches it. A piercing shot pops it, plowing it like an unbreakable and
    ///     shattering it at the next wall, and so does a laser beam. Colourless and scoreless, so it never counts
    ///     toward a goal or moves the streak, and it resists paint. Not an <see cref="UnbreakableBalloonModel" />:
    ///     that would pull it into formations and roaming. Each subclass differs only in what its pop pays.
    /// </summary>
    internal abstract class PlatedBalloonModel : BalloonModelBase, IHasDeflectStamp, IResistsPaint, IDeflectsShots
    {
        public float DeflectStampScale { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }
        public bool DeflectsOrdinaryHit => true;

        protected PlatedBalloonModel(BalloonModelConfig config)
            : base(config)
        {
            DeflectStampScale = config.DeflectStampScale;
            NudgeOverrides = config.NudgeOverrides;
        }

        // An item's damage carries no DirectHit, and a bomb's neighbours carry Piercing as the laser does, so the
        // shot's pop needs both: a bomb beside it must not pay out. The laser's beam carries Beam, which a blast
        // never does. None leaves any other item's hit without a bounce.
        public override HitOutcome EvaluateHit(DamageContext context)
        {
            if ((context.Flags & DamageFlags.Beam) != 0)
            {
                return HitOutcome.Pop;
            }

            if ((context.Flags & DamageFlags.DirectHit) == 0)
            {
                return HitOutcome.None;
            }

            return (context.Flags & DamageFlags.Piercing) != 0 ? HitOutcome.Pop : HitOutcome.Deflect;
        }
    }
}
