namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Injectable seam over the static <see cref="Cinematic"/> state, so consumers can gate on an
    ///     in-progress cinematic under test. Behaviour questions go through <see cref="Has"/> — the
    ///     current cinematic's <see cref="CinematicTraits"/> are declared once per state in
    ///     <c>Configuration/CinematicsSettings</c>, so consumers never enumerate states.
    /// </summary>
    internal interface ICinematicState
    {
        bool IsPlaying { get; }

        /// <summary>True while the current cinematic declares <paramref name="trait"/>.</summary>
        bool Has(CinematicTraits trait);
    }
}
