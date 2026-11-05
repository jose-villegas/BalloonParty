using BalloonParty.Projectile;
using BalloonParty.Shared;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    // A wind loop whose volume tracks cruise speed — of the fastest cruising ball, so one loop serves
    // every ball in flight. Cruise speed is a pure function of TotalCruiseTaps
    // (sweep + wall-bounce taps both feed it), so this reads that off the live projectile and maps it to
    // 0-1: 0 at the first tap, 1 at the tap count that reaches the cruise speed cap. The loop starts at
    // the first tap and stops when cruising ends OR the shot dies — both clear IsCruising, so a single
    // gate covers both. Each new step ramps over the entry's FadeInSeconds; the stop fades out.
    internal sealed class WindSoundRouter : ITickable
    {
        private readonly ISoundPlayer _player;
        private readonly IProjectileRegistry _projectiles;
        private readonly float _maxTaps;

        private SoundHandle _handle = SoundHandle.None;
        private int _lastTaps = -1;

        public WindSoundRouter(ISoundPlayer player, IProjectileRegistry projectiles,
            IProjectileFlightConfig flightConfig)
        {
            _player = player;
            _projectiles = projectiles;

            // Taps at full speed. Taps stop accruing once piercing arms, so the arming threshold IS the
            // ceiling; only with piercing disabled does the speed cap bind instead, and converting that
            // cap to a tap count is a plain divide — SpeedGainPerTap is a FRACTION of base speed, so
            // multiplying by ProjectileSpeed (as this used to) inflated the ceiling ~8x and left the loop
            // stuck near silent.
            var gainPerTap = Mathf.Max(0.0001f, flightConfig.SpeedGainPerTap);
            _maxTaps = flightConfig.CruisePiercingTapThreshold > 0
                ? flightConfig.CruisePiercingTapThreshold
                : Mathf.Max(1f, (flightConfig.MaxSpeedMultiplier - 1f) / gainPerTap);
        }

        public void Tick()
        {
            var taps = MaxCruiseTaps();

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

        // A shot that died or landed is gone from the registry, so its wind stops with it.
        private int MaxCruiseTaps()
        {
            var taps = 0;
            for (var i = 0; i < _projectiles.InFlightCount; i++)
            {
                var projectile = _projectiles.InFlightAt(i);
                if (projectile.IsCruising.Value)
                {
                    taps = Mathf.Max(taps, projectile.Flight.TotalCruiseTaps);
                }
            }

            return taps;
        }
    }
}
