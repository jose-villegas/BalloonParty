namespace BalloonParty.Shared.GameState
{
    internal class CinematicStateService : ICinematicState
    {
        public bool IsPlaying => Cinematic.IsPlaying;
    }
}
