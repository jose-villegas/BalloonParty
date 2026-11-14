using BalloonParty.Balloon.Model;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     A reward balloon's pop, read from a hit. The payout pays these and the service rolls for another golden
    ///     balloon on them, so both read a pop the same way: what one pays, the other counts.
    /// </summary>
    internal static class RewardBalloonPop
    {
        internal static bool TryGet(ActorHitMessage msg, out IBalloonModel balloon)
        {
            if (msg.Outcome == HitOutcome.Pop && msg.Actor is IBalloonModel model && model.TypeName.IsReward())
            {
                balloon = model;
                return true;
            }

            balloon = null;
            return false;
        }
    }
}
