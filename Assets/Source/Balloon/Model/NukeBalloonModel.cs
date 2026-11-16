using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     The nuke reward balloon (PLAN-NukeBalloon): a <see cref="PlatedBalloonModel" /> that is never popped by a
    ///     hit. It is <see cref="Armed" /> instead, and <c>NukeFuse</c> sets it off when its fuse runs out. The shot's
    ///     ordinary hit dents it: it bounces off and costs one of the catalogue entry's hits, and the last one arms it.
    ///     It is a rescue, and the player who needs one rarely has a pierce. Piercing damage or a beam arms it at once.
    ///     Armed, it is a lead casing with a fuse running: it deflects every ball, piercing or not, takes no more
    ///     damage and is anchored where it stands, so the counter is read where it is.
    /// </summary>
    internal sealed class NukeBalloonModel : PlatedBalloonModel, IHasDurability, IBalanceAnchor
    {
        private readonly ReactiveProperty<bool> _armed = new(false);
        private readonly ReactiveProperty<int> _fuseSecondsLeft = new(0);

        int IHasDurability.MaxHitPoints => MaxHitPoints;
        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        // As the tough balloon's: the last ordinary hit lands rather than being plowed, so a pierce arms it then.
        // Armed, its hits are 0, so a pierce meets it on contact and bounces off.
        public override bool DeflectsOrdinaryHit => HitsRemaining.Value > 1;

        public bool IsAnchored => _armed.Value;

        internal IReadOnlyReactiveProperty<bool> Armed => _armed;

        /// <summary>The whole seconds its fuse has left, for the counter on its body; 0 before it is armed.</summary>
        internal IReadOnlyReactiveProperty<int> FuseSecondsLeft => _fuseSecondsLeft;

        protected override HitOutcome SurviveOutcome => HitOutcome.Deflect;

        internal NukeBalloonModel(BalloonModelConfig config)
            : base(config)
        {
        }

        public override HitOutcome EvaluateHit(DamageContext context)
        {
            var direct = (context.Flags & DamageFlags.DirectHit) != 0;
            if (_armed.Value)
            {
                return direct ? HitOutcome.Deflect : HitOutcome.None;
            }

            if ((context.Flags & (DamageFlags.Piercing | DamageFlags.Beam)) != 0)
            {
                Arm();
                return direct ? HitOutcome.Deflect : HitOutcome.None;
            }

            if (!direct)
            {
                return HitOutcome.None;
            }

            if (EvaluateNormalHit(context) == HitOutcome.Pop)
            {
                Arm();
            }

            return HitOutcome.Deflect;
        }

        internal void SetFuseSecondsLeft(int seconds)
        {
            _fuseSecondsLeft.Value = Mathf.Max(0, seconds);
        }

        private void Arm()
        {
            HitsRemaining.Value = 0;
            _armed.Value = true;
        }
    }
}
