namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Injectable seam over the static <see cref="Cinematic"/> playing flag, so the
    ///     run lifecycle can gate against an in-progress cinematic under test.
    /// </summary>
    internal interface ICinematicState
    {
        bool IsPlaying { get; }
    }
}
