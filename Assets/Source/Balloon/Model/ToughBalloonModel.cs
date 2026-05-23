using BalloonParty.Slots.Capabilities;
using UniRx;

namespace BalloonParty.Balloon.Model
{
    internal class ToughBalloonModel : BalloonModelBase, IHasDurability, IHasScore
    {
        public int ScoreValue { get; }

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        internal ToughBalloonModel(BalloonModelConfig config) : base(config)
        {
            ScoreValue = config.ScoreValue;
        }

        protected override HitOutcome EvaluateNormalHit(DamageContext context)
        {
            var survives = HitsRemaining.Value - context.Damage > 0;
            HitsRemaining.Value -= context.Damage;
            return survives ? HitOutcome.Deflect : HitOutcome.Pop;
        }
    }
}
