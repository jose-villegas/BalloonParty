namespace BalloonParty.Shared.Messages
{
    public readonly struct LevelUpFillTrailsMessage
    {
        public readonly int TrailsPerBar;
        public readonly float StaggerDelay;

        public LevelUpFillTrailsMessage(int trailsPerBar, float staggerDelay)
        {
            TrailsPerBar = trailsPerBar;
            StaggerDelay = staggerDelay;
        }
    }
}
