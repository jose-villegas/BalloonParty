using System;
using BalloonParty.Shared.GameState;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    // Ambient music tied to navigation. The Launch state gets a loop (author LaunchMusic as a loop on
    // the Music channel); leaving Launch — pressing Play into Game — stops it, fading out per the entry's
    // FadeOutSeconds. Kept as its own router so future per-state music slots in here.
    internal sealed class MusicSoundRouter : IStartable, IDisposable
    {
        private readonly ISoundPlayer _player;
        private readonly INavigation _navigation;
        private readonly CompositeDisposable _subscriptions = new();

        private SoundHandle _launchHandle = SoundHandle.None;

        public MusicSoundRouter(ISoundPlayer player, INavigation navigation)
        {
            _player = player;
            _navigation = navigation;
        }

        public void Start()
        {
            _navigation.Current.Subscribe(OnNavigationChanged).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnNavigationChanged(NavigationState state)
        {
            if (state == NavigationState.Launch)
            {
                if (!_launchHandle.IsValid)
                {
                    _launchHandle = _player.Play(GameSoundId.LaunchMusic, null);
                }
            }
            else if (_launchHandle.IsValid)
            {
                _player.Stop(_launchHandle);
                _launchHandle = SoundHandle.None;
            }
        }
    }
}
