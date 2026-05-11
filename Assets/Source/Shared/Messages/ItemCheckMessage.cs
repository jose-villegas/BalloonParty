#region

using System.Collections.Generic;
using BalloonParty.Balloon.Model;

#endregion

namespace BalloonParty.Shared.Messages
{
    public readonly struct ItemCheckMessage
    {
        public readonly IReadOnlyList<IWriteableBalloonModel> NewBalloons;
        public readonly int TurnCount;

        public ItemCheckMessage(IReadOnlyList<IWriteableBalloonModel> newBalloons, int turnCount)
        {
            NewBalloons = newBalloons;
            TurnCount = turnCount;
        }
    }
}
