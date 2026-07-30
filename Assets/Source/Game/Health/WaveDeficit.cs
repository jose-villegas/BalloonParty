namespace BalloonParty.Game.Health
{
    /// <summary>Result of the per-wave deficit calculation: how many hearts the wave costs and how many slots go unspawned.</summary>
    public readonly struct WaveDeficit
    {
        /// <summary>Full lines of deficit — each costs one heart.</summary>
        public readonly int HeartsLost;

        /// <summary>Total balloon slots that won't spawn this wave (includes both heart-line slots and partial remainder).</summary>
        public readonly int UnspawnedSlots;

        public WaveDeficit(int heartsLost, int unspawnedSlots)
        {
            HeartsLost = heartsLost;
            UnspawnedSlots = unspawnedSlots;
        }
    }
}
