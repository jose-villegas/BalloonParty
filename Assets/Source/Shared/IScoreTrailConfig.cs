namespace BalloonParty.Shared
{
    /// <summary>Score trail presentation: timing, scatter, burst, and pool prewarm sizes.</summary>
    public interface IScoreTrailConfig
    {
        float ScorePointTraceDuration { get; }
        float ScorePointsScatterDelay { get; }
        float ScorePointBurstDuration { get; }
        int ScoreTrailPrewarmPerColor { get; }
        int ProgressNoticePrewarmPerColor { get; }
    }
}
