using System.Collections.Generic;
using BalloonParty.Balloon.Model;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ItemCheckMessage
    {
        public readonly IReadOnlyList<IBalloonModel> NewBalloons;
        // Waves spawned so far this level, this one included; the item cadence counts these. 0 on an initial fill.
        public readonly int Wave;

        // Fresh-board fill (level start/transition); bypasses the cadence gate.
        public readonly bool IsInitialSpawn;

        public ItemCheckMessage(IReadOnlyList<IBalloonModel> newBalloons, int wave, bool isInitialSpawn)
        {
            NewBalloons = newBalloons;
            Wave = wave;
            IsInitialSpawn = isInitialSpawn;
        }
    }
}
