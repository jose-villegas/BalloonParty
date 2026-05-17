namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Implemented by services that should pause/resume work when a
    ///     cinematic begins or ends. Register implementations with VContainer;
    ///     <see cref="Cinematic"/> drives the callbacks automatically.
    /// </summary>
    internal interface ICinematicAware
    {
        void OnCinematicBegin(CinematicState state);
        void OnCinematicEnd();
    }
}

