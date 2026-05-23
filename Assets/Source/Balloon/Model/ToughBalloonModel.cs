using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Balloon.Model
{
    internal class ToughBalloonModel : BalloonModelBase
    {
        internal ToughBalloonModel(BalloonModelConfig config) : base(config) { }

        protected override HitOutcome EvaluateNormalHit(DamageContext context)
        {
            var survives = HitsRemaining.Value - context.Damage > 0;
            HitsRemaining.Value -= context.Damage;
            return survives ? HitOutcome.Deflect : HitOutcome.Pop;
        }
    }
}
