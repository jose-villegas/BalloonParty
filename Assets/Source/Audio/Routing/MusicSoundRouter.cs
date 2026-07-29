using System;
using BalloonParty.Game.Danger;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    // Ambient music tied to navigation. Launch gets its own loop; entering Game starts both a day and
    // a night gameplay loop. Only the active one plays at full volume — the other is ducked to its
    // VolumeRange.x — crossfading smoothly when IsNight flips. Both duck during flight and popups.
    //
    // Pitch drops proportionally with danger level (0→tritone at max danger).
    internal sealed class MusicSoundRouter : IStartable, ITickable, IDisposable
    {
        private const float DangerMaxSemitones = 6f;
        private const float PitchLerpSeconds = 0.4f;

        private readonly ISoundPlayer _player;
        private readonly INavigation _navigation;
        private readonly IDangerLevel _dangerLevel;
        private readonly ITimeOfDayNight _timeOfDayNight;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<ProjectileFiredMessage> _firedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private SoundHandle _launchHandle = SoundHandle.None;
        private SoundHandle _dayHandle = SoundHandle.None;
        private SoundHandle _nightHandle = SoundHandle.None;
        private bool _inFlight;
        private bool _isDucked;
        private bool _isPlaying;
        private bool _wasNight;

        public MusicSoundRouter(
            ISoundPlayer player,
            INavigation navigation,
            IDangerLevel dangerLevel,
            ITimeOfDayNight timeOfDayNight,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            ISubscriber<ProjectileFiredMessage> firedSubscriber)
        {
            _player = player;
            _navigation = navigation;
            _dangerLevel = dangerLevel;
            _timeOfDayNight = timeOfDayNight;
            _loadedSubscriber = loadedSubscriber;
            _destroyedSubscriber = destroyedSubscriber;
            _firedSubscriber = firedSubscriber;
        }

        public void Start()
        {
            _navigation.Current.Subscribe(OnNavigationChanged).AddTo(_subscriptions);
            _dangerLevel.Level.Subscribe(_ => ApplyPitch()).AddTo(_subscriptions);
            _loadedSubscriber.Subscribe(_ => OnFlightEnded()).AddTo(_subscriptions);
            _destroyedSubscriber.Subscribe(_ => OnFlightEnded()).AddTo(_subscriptions);
            _firedSubscriber.Subscribe(_ => OnFired()).AddTo(_subscriptions);
        }

        public void Tick()
        {
            if (!_isPlaying)
            {
                return;
            }

            // Detect loops silently killed by voice-limiter stealing or StopAllVoices. Clear the
            // stale handle so StartGameplay re-plays the loop, restoring music seamlessly.
            var revived = false;
            if (_dayHandle.IsValid && !_player.IsAlive(_dayHandle))
            {
                _dayHandle = SoundHandle.None;
                revived = true;
            }

            if (_nightHandle.IsValid && !_player.IsAlive(_nightHandle))
            {
                _nightHandle = SoundHandle.None;
                revived = true;
            }

            if (revived)
            {
                StartGameplay();
                ApplyVolume();
                ApplyPitch();
            }

            var night = _timeOfDayNight.IsNight;
            if (night != _wasNight)
            {
                _wasNight = night;
                ApplyVolume();
            }
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
                    StartGameplay();
                    ApplyVolume();
                    ApplyPitch();
                    break;

                default:
                    _isDucked = true;
                    ApplyVolume();
                    break;
            }
        }

        private void OnFired()
        {
            _inFlight = true;
            ApplyVolume();
        }

        private void OnFlightEnded()
        {
            _inFlight = false;
            ApplyVolume();
        }

        private void StartGameplay()
        {
            if (!_dayHandle.IsValid)
            {
                _dayHandle = _player.Play(GameSoundId.GameplayLoopDay, null);
            }

            if (!_nightHandle.IsValid)
            {
                _nightHandle = _player.Play(GameSoundId.GameplayLoopNight, null);
            }

            _wasNight = _timeOfDayNight.IsNight;
            _isPlaying = true;
        }

        private void ApplyPitch()
        {
            var semitones = _dangerLevel.Level.Value * DangerMaxSemitones;
            var pitch = (-semitones).SemitonesToPitchMultiplier();

            if (_dayHandle.IsValid)
            {
                _player.SetPitch(_dayHandle, pitch, PitchLerpSeconds);
            }

            if (_nightHandle.IsValid)
            {
                _player.SetPitch(_nightHandle, pitch, PitchLerpSeconds);
            }
        }

        // Volume ducking uses SetVolumeFactor which lerps between the bank entry's VolumeRange
        // (min = ducked floor, max = full volume). Factor 0 = VolumeRange.x, factor 1 = VolumeRange.y.
        // The active time-of-day loop gets factor 1 (or 0 if ducked/in-flight); the inactive one always 0.
        private void ApplyVolume()
        {
            var ducked = _inFlight || _isDucked;
            var night = _timeOfDayNight.IsNight;
            var dayFactor = (!ducked && !night) ? 1f : 0f;
            var nightFactor = (!ducked && night) ? 1f : 0f;

            if (_dayHandle.IsValid)
            {
                _player.SetVolumeFactor(_dayHandle, dayFactor);
            }

            if (_nightHandle.IsValid)
            {
                _player.SetVolumeFactor(_nightHandle, nightFactor);
            }
        }

        private void StopGameplay()
        {
            if (_dayHandle.IsValid)
            {
                _player.Stop(_dayHandle);
                _dayHandle = SoundHandle.None;
            }

            if (_nightHandle.IsValid)
            {
                _player.Stop(_nightHandle);
                _nightHandle = SoundHandle.None;
            }

            _isPlaying = false;
        }
    }
}
