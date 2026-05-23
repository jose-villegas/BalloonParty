using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Balloon.Model
{
    internal class UnbreakableBalloonModel : BalloonModelBase
    {
        // No IHasDurability — this is a permanent obstacle; hits never deplete it.
        // No IHasScore — destroying via Piercing is intended to be a tool-cost, not a reward.
        // No nudge overrides — permanent obstacles don't react to nudge animations.
        public override IReadOnlyList<NudgeOverride> NudgeOverrides => null;

        internal UnbreakableBalloonModel(BalloonModelConfig config) : base(config) { }

        public override HitOutcome EvaluateHit(DamageContext context)
        {
            // Piercing is the only thing that pops this balloon, but we deliberately
            // do not mutate HitsRemaining — there is no durability state to track.
            if (context.Flags.HasFlag(DamageFlags.Piercing))
            {
                return HitOutcome.Pop;
            }

            return HitOutcome.Deflect;
        }
    }
}
