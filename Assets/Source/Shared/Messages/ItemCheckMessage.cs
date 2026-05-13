using System.Collections.Generic;
using BalloonParty.Balloon.Model;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ItemCheckMessage
    {
        public readonly IReadOnlyList<IBalloonModel> NewBalloons;
        public readonly int TurnCount;

        public ItemCheckMessage(IReadOnlyList<IBalloonModel> newBalloons, int turnCount)
        {
            NewBalloons = newBalloons;
            TurnCount = turnCount;
        }
    }
}
