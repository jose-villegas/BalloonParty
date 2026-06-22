namespace BalloonParty.Shared.GameState
{
    internal class CinematicStateService : ICinematicState
    {
        public bool IsPlaying => Cinematic.IsPlaying;

        public bool BlocksLoss => Cinematic.Current.Value
            is CinematicState.LevelUpPanIn
            or CinematicState.LevelUpRestore;
    }
}
