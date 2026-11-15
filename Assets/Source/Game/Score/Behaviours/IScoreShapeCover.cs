namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>How much of the board the BigScore shapes being drawn in the air take up right now.</summary>
    internal interface IScoreShapeCover
    {
        /// <summary>
        ///     The summed area, in world units squared, of every active formation's bounding circle at its current
        ///     scale. Overlapping shapes count twice, so this reads high when they bunch up.
        /// </summary>
        float CoveredArea { get; }
    }
}
