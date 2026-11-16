using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     A plated reward balloon (the golden balloon, the disco ball): it takes hits as an unbreakable does. The
    ///     shot's own ordinary hit bounces off it, and any piercing hit pops it: a piercing shot (plowing it and
    ///     shattering it at the next wall), a bomb's blast beside it, or a laser beam. Colourless and scoreless, so it never counts
    ///     toward a goal or moves the streak, and it resists paint. Not an <see cref="UnbreakableBalloonModel" />:
    ///     that would pull it into formations and roaming. Each subclass differs in what its pop pays, and the nuke
    ///     also wears down under ordinary hits (<see cref="NukeBalloonModel" />).
    /// </summary>
    internal abstract class PlatedBalloonModel : BalloonModelBase, IHasDeflectStamp, IResistsPaint, IDeflectsShots
    {
        public float DeflectStampScale { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }
        public virtual bool DeflectsOrdinaryHit => true;

        protected PlatedBalloonModel(BalloonModelConfig config)
            : base(config)
        {
            DeflectStampScale = config.DeflectStampScale;
            NudgeOverrides = config.NudgeOverrides;
        }

        // Beam counts too, so a laser configured without Piercing still breaks it. An item's plain damage (lightning,
        // paint) carries no DirectHit, and None leaves it without a bounce.
        public override HitOutcome EvaluateHit(DamageContext context)
        {
            if ((context.Flags & (DamageFlags.Piercing | DamageFlags.Beam)) != 0)
            {
                return HitOutcome.Pop;
            }

            return (context.Flags & DamageFlags.DirectHit) != 0 ? HitOutcome.Deflect : HitOutcome.None;
        }
    }
}
