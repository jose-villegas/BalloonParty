using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Balloon.Model
{
    internal class ToughBalloonModel : BalloonModelBase
    {
        internal ToughBalloonModel(BalloonModelConfig config) : base(config) { }

        public override HitOutcome EvaluateHit(int damage)
        {
            var survives = HitsRemaining.Value - damage > 0;
            HitsRemaining.Value -= damage;
            return survives ? HitOutcome.Deflect : HitOutcome.Pop;
        }
    }
}
