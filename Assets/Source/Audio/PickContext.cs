namespace BalloonParty.Audio
{
    internal readonly struct PickContext
    {
        public readonly int Streak;
        public readonly int CurrentSemitone;
        public readonly int BurstIndex;
        public readonly float NormalizedPan;

        public PickContext(int streak, int currentSemitone, int burstIndex, float normalizedPan)
        {
            Streak = streak;
            CurrentSemitone = currentSemitone;
            BurstIndex = burstIndex;
            NormalizedPan = normalizedPan;
        }
    }
}
