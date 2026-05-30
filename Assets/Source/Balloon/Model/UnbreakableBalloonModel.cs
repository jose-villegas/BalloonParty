using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Balloon.Model
{
    internal class UnbreakableBalloonModel : BalloonModelBase, IHasScore, IHasScoreColor
    {
        public int ScoreValue { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        internal UnbreakableBalloonModel(BalloonModelConfig config) : base(config)
        {
            ScoreValue = config.ScoreValue;
            NudgeOverrides = config.NudgeOverrides;
        }

        public void ResolveScoreAttribution(in DamageContext context, IList<ScoreAttribution> results)
        {
            if (!string.IsNullOrEmpty(context.SourceColorId))
            {
                results.Add(new ScoreAttribution(context.SourceColorId, ScoreValue));
            }
        }

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
