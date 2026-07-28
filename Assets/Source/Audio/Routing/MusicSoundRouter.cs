using System;
using BalloonParty.Game.Danger;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    // Ambient music tied to navigation. The Launch state gets a loop (author LaunchMusic as a loop on
    // the Music channel); entering Game starts the GameplayLoop; leaving Game (level-up or game-over)
    // ducks it rather than stopping. Returning to Game restores full volume.
    //
    // Pitch drops proportionally with danger level (0→tritone at max danger) and by another tritone
    // when the projectile enters its doomed last-breath segment. Resets on projectile load or death.
    internal sealed class MusicSoundRouter : IStartable, IDisposable
    {
        private const float DangerMaxSemitones = 6f;
        private const float DoomedSemitones = 6f;
        private const float SemitoneRatio = 1f / 12f;
        private const float PitchLerpSeconds = 0.4f;

        private readonly ISoundPlayer _player;
        private readonly INavigation _navigation;
        private readonly IDangerLevel _dangerLevel;
        private readonly ISubscriber<ProjectileDoomedStartedMessage> _doomedStartedSubscriber;
        private readonly ISubscriber<ProjectileDoomedEndedMessage> _doomedEndedSubscriber;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private SoundHandle _launchHandle = SoundHandle.None;
        private SoundHandle _gameplayHandle = SoundHandle.None;
        private bool _isDoomed;
        private bool _inFlight;
        private bool _isDucked;

        public MusicSoundRouter(
            ISoundPlayer player,
            INavigation navigation,
            IDangerLevel dangerLevel,
            ISubscriber<ProjectileDoomedStartedMessage> doomedStartedSubscriber,
            ISubscriber<ProjectileDoomedEndedMessage> doomedEndedSubscriber,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber)
        {
            _player = player;
            _navigation = navigation;
            _dangerLevel = dangerLevel;
            _doomedStartedSubscriber = doomedStartedSubscriber;
            _doomedEndedSubscriber = doomedEndedSubscriber;
            _loadedSubscriber = loadedSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _firedSubscriber = firedSubscriber;
        }

        public void Start()
        {
            _navigation.Current.Subscribe(OnNavigationChanged).AddTo(_subscriptions);
            _dangerLevel.Level.Subscribe(_ => ApplyPitch()).AddTo(_subscriptions);
            _doomedStartedSubscriber.Subscribe(_ => OnDoomedChanged(true)).AddTo(_subscriptions);
            _doomedEndedSubscriber.Subscribe(_ => OnDoomedChanged(false)).AddTo(_subscriptions);
            _loadedSubscriber.Subscribe(_ => OnFlightEnded()).AddTo(_subscriptions);
            _destroyedSubscriber.Subscribe(_ => OnFlightEnded()).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(_ => OnFired()).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnNavigationChanged(NavigationState state)
        {
            switch (state)
            {
                case NavigationState.Launch:
                    StopGameplay();
                    if (!_launchHandle.IsValid)
                    {
                        _launchHandle = _player.Play(GameSoundId.LaunchMusic, null);
                    }

                    break;

                case NavigationState.Game:
                    if (_launchHandle.IsValid)
                    {
                        _player.Stop(_launchHandle);
                        _launchHandle = SoundHandle.None;
                    }

                    _isDucked = false;

                    if (!_gameplayHandle.IsValid)
                    {
                        _gameplayHandle = _player.Play(GameSoundId.GameplayLoop, null);
                    }

                    ApplyVolume();
                    ApplyPitch();
                    break;

                default:
                    // LevelUp, GameOver — duck but keep playing.
                    _isDucked = true;
                    ApplyVolume();
                    break;
            }
        }

        private void OnDoomedChanged(bool doomed)
        {
            _isDoomed = doomed;
            ApplyPitch();
        }

        private void OnFired()
        {
            _inFlight = true;
            ApplyVolume();
        }

        private void OnFlightEnded()
        {
            _isDoomed = false;
            _inFlight = false;
            ApplyVolume();
            ApplyPitch();
        }

        private void ApplyPitch()
        {
            if (!_gameplayHandle.IsValid)
            {
                return;
            }

            var dangerSemitones = _dangerLevel.Level.Value * DangerMaxSemitones;
            var doomedSemitones = _isDoomed ? DoomedSemitones : 0f;
            var totalSemitones = dangerSemitones + doomedSemitones;
            var pitch = Mathf.Pow(2f, -totalSemitones * SemitoneRatio);
            _player.SetPitch(_gameplayHandle, pitch, PitchLerpSeconds);
        }

        // Volume ducking uses SetVolumeFactor which lerps between the bank entry's VolumeRange
        // (min = ducked floor, max = full volume). Factor 0 = VolumeRange.x, factor 1 = VolumeRange.y.
        private void ApplyVolume()
        {
            if (!_gameplayHandle.IsValid)
            {
                return;
            }

            var factor = (_inFlight || _isDucked) ? 0f : 1f;
            _player.SetVolumeFactor(_gameplayHandle, factor);
        }

        private void StopGameplay()
        {
            if (_gameplayHandle.IsValid)
            {
                _player.Stop(_gameplayHandle);
                _gameplayHandle = SoundHandle.None;
            }
        }
    }
}
