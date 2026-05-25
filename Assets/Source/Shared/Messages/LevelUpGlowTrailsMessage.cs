namespace BalloonParty.Shared.Messages
{
    public readonly struct LevelUpGlowTrailsMessage
    {
        public readonly int TrailsPerBar;
        public readonly float StaggerDelay;

        public LevelUpGlowTrailsMessage(int trailsPerBar, float staggerDelay)
        {
            TrailsPerBar = trailsPerBar;
            StaggerDelay = staggerDelay;
        }
    }
}
