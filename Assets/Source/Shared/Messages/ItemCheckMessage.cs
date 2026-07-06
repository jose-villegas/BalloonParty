using System.Collections.Generic;
using BalloonParty.Balloon.Model;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ItemCheckMessage
    {
        public readonly IReadOnlyList<IBalloonModel> NewBalloons;
        public readonly int TurnCount;

        // Fresh-board fill (level start/transition); bypasses the turn-cadence gate.
        public readonly bool IsInitialSpawn;

        public ItemCheckMessage(IReadOnlyList<IBalloonModel> newBalloons, int turnCount, bool isInitialSpawn)
        {
            NewBalloons = newBalloons;
            TurnCount = turnCount;
            IsInitialSpawn = isInitialSpawn;
        }
    }
}
