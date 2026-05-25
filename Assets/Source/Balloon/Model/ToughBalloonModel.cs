using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    internal class ToughBalloonModel : BalloonModelBase, IHasDurability, IHasScore, IHasScoreColor
    {
        public int ScoreValue { get; }
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal ToughBalloonModel(BalloonModelConfig config) : base(config)
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

        protected override HitOutcome EvaluateNormalHit(DamageContext context)
        {
            var survives = HitsRemaining.Value - context.Damage > 0;
            HitsRemaining.Value -= context.Damage;
            return survives ? HitOutcome.Deflect : HitOutcome.Pop;
        }
    }
}
