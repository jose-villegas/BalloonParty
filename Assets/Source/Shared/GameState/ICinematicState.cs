namespace BalloonParty.Shared.GameState
{
    /// <summary>Injectable seam over the static <see cref="Cinematic"/> state, testable without global state.</summary>
    internal interface ICinematicState
    {
        bool IsPlaying { get; }

        /// <summary>True while the current cinematic declares <paramref name="trait"/>.</summary>
        bool Has(CinematicTraits trait);
    }
}
