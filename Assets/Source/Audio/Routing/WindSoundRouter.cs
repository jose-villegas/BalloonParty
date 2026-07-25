using System;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    // A wind loop whose volume tracks cruise speed. Cruise speed is a pure function of TotalCruiseTaps
    // (sweep + wall-bounce taps both feed it), so this reads that off the live projectile and maps it to
    // 0-1: 0 at the first tap, 1 at the tap count that reaches the cruise speed cap. The loop starts at
    // the first tap and stops when cruising ends OR the shot dies — both clear IsCruising, so a single
    // gate covers both. Each new step ramps over the entry's FadeInSeconds; the stop fades out.
    internal sealed class WindSoundRouter : IStartable, ITickable, IDisposable
    {
        private readonly ISoundPlayer _player;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly float _maxTaps;
        private readonly CompositeDisposable _subscriptions = new();

        private IProjectileModel _projectile;
        private SoundHandle _handle = SoundHandle.None;
        private int _lastTaps = -1;

        public WindSoundRouter(ISoundPlayer player, ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            IProjectileFlightConfig flightConfig)
        {
            _player = player;
            _loadedSubscriber = loadedSubscriber;

            // Taps to reach full speed: the per-tap bonus (CruiseSpeedPerShield) caps the speed at
            // base x MaxCruiseSpeedMultiplier, i.e. the bonus caps at base x (mult - 1). Mirrors the map
            // in ProjectileMotionResolver.ResolveFlightSpeed.
            var perShield = Mathf.Max(0.0001f, flightConfig.CruiseSpeedPerShield);
            _maxTaps = Mathf.Max(1f,
                flightConfig.ProjectileSpeed * (flightConfig.MaxCruiseSpeedMultiplier - 1f) / perShield);
        }

        public void Start()
        {
            _loadedSubscriber.Subscribe(OnLoaded).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        public void Tick()
        {
            var taps = _projectile != null && _projectile.IsCruising.Value
                ? _projectile.Flight.TotalCruiseTaps
                : 0;

            if (taps >= 1)
            {
                if (!_handle.IsValid)
                {
                    _handle = _player.Play(GameSoundId.WindLoop, null);
                    _lastTaps = -1;
                }

                if (taps != _lastTaps)
                {
                    _lastTaps = taps;
                    var factor = Mathf.Clamp01((taps - 1f) / Mathf.Max(1f, _maxTaps - 1f));
                    _player.SetVolumeFactor(_handle, factor);
                }
            }
            else if (_handle.IsValid)
            {
                _player.Stop(_handle);
                _handle = SoundHandle.None;
                _lastTaps = -1;
            }
        }

        private void OnLoaded(ProjectileLoadedMessage message)
        {
            // Fresh shot: silence any wind still tracking the old one, then follow the new model. The
            // loop won't sound until this shot earns its first tap.
            if (_handle.IsValid)
            {
                _player.Stop(_handle);
                _handle = SoundHandle.None;
            }

            _projectile = message.Model;
            _lastTaps = -1;
        }
    }
}
