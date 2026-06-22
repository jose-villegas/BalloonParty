namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Injectable seam over the static <see cref="Cinematic"/> playing flag, so the
    ///     run lifecycle can gate against an in-progress cinematic under test.
    /// </summary>
    internal interface ICinematicState
    {
        bool IsPlaying { get; }

        /// <summary>
        ///     True only while a cinematic that must not be interrupted by a loss is playing (the
        ///     level-up states). The heart-drain cinematic is <em>not</em> loss-blocking — game-over
        ///     fires at 0 HP even while it runs.
        /// </summary>
        bool BlocksLoss { get; }
    }
}
