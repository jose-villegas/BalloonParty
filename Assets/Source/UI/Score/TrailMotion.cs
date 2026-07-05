namespace BalloonParty.UI.Score
{
    /// <summary>
    ///     Selects which move/scale curve pair a <see cref="FlyingTrail" /> flies with. The trail holds a
    ///     collection indexed by this enum (O(1) lookup); a value with no curve set falls back to the
    ///     trail's default pair, so adding a motion is opt-in per prefab and per call site.
    /// </summary>
    public enum TrailMotion
    {
        Default = 0,
        Return = 1,
        Fall = 2,
    }
}
