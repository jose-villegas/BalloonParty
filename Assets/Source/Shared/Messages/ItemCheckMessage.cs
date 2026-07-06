using System.Collections.Generic;
using BalloonParty.Balloon.Model;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ItemCheckMessage
    {
        public readonly IReadOnlyList<IBalloonModel> NewBalloons;
        public readonly int TurnCount;

        // The one-off fill of a fresh board (level start / transition). Seeds a separate item count
        // and bypasses the turn-cadence gate — it isn't a "turn".
        public readonly bool IsInitialSpawn;

        public ItemCheckMessage(IReadOnlyList<IBalloonModel> newBalloons, int turnCount, bool isInitialSpawn)
        {
            NewBalloons = newBalloons;
            TurnCount = turnCount;
            IsInitialSpawn = isInitialSpawn;
        }
    }
}
