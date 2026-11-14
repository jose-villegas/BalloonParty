using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     The heart reward balloon (PLAN-RewardBalloons): a heart wrapped in bandages that gives a heart back. Any
    ///     hit pops it, the shot's or an item's, and the pop heals one heart (<c>RewardBalloonPayout</c>). Colourless
    ///     and scoreless, so it never counts toward a goal or moves the streak, and it resists paint.
    /// </summary>
    internal sealed class HeartBalloonModel : BalloonModelBase, IResistsPaint
    {
        public override IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        internal HeartBalloonModel(BalloonModelConfig config)
            : base(config)
        {
            NudgeOverrides = config.NudgeOverrides;
        }

        // One hit, whatever the entry's hit count says: help that took two shots would read as a tough balloon.
        public override HitOutcome EvaluateHit(DamageContext context)
        {
            HitsRemaining.Value = 0;
            return HitOutcome.Pop;
        }
    }
}
