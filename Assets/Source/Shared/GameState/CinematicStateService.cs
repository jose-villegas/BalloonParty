using BalloonParty.Configuration;

namespace BalloonParty.Shared.GameState
{
    internal class CinematicStateService : ICinematicState
    {
        private readonly ICinematicsSettings _settings;

        public CinematicStateService(ICinematicsSettings settings)
        {
            _settings = settings;
        }

        public bool IsPlaying => Cinematic.IsPlaying;

        public bool Has(CinematicTraits trait)
        {
            return (_settings.EntryOf(Cinematic.Current.Value).Traits & trait) != 0;
        }
    }
}
