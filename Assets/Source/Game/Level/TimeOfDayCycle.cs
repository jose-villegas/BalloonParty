using System;
using BalloonParty.Configuration.Effects;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.SceneLight;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Night-mode time-of-day driver: walks the ambient light direction around the circle as the
    ///     player climbs. Level 1 sits at the authored rest direction; each further level advances by
    ///     <see cref="ITimeOfDaySettings.DegreesPerLevel"/>, and the level-up transition sweeps between
    ///     them — always-forward (the angle is continuous, never wrapped, so it never reverses across the
    ///     midnight→dawn seam) and on unscaled time so it plays through the transition pause. Does nothing
    ///     unless <see cref="ITimeOfDaySettings.NightModeEnabled"/>, leaving the light at its authored
    ///     direction. Owns only the cycle policy; the ambient state/push lives on
    ///     <see cref="TimeOfDayService"/> (see @ref plan_night_mode).
    /// </summary>
    internal sealed class TimeOfDayCycle : IStartable, ITickable, IRunResettable, IDisposable
    {
        private readonly ITimeOfDaySettings _settings;
        private readonly ISceneLightSettings _lightSettings;
        private readonly ILevelProgress _levelProgress;
        private readonly TimeOfDayService _service;
        private readonly CompositeDisposable _subscriptions = new();

        private float _currentAngle;
        private float _sweepFromAngle;
        private float _sweepToAngle;
        private float _sweepElapsed;
        private bool _sweeping;

        // After LevelController (Score) so the run's start level is already in place when we snap — it may
        // be > 1 via the StartLevel dev cheat, so we must read the reset value, not assume level 1.
        public int ResetOrder => RunResetOrder.Respawn;

        internal TimeOfDayCycle(
            ITimeOfDaySettings settings, ISceneLightSettings lightSettings,
            ILevelProgress levelProgress, TimeOfDayService service)
        {
            _settings = settings;
            _lightSettings = lightSettings;
            _levelProgress = levelProgress;
            _service = service;
        }

        void IStartable.Start()
        {
            if (!_settings.NightModeEnabled)
            {
                return;
            }

            // Only a level-up transition sweeps; the initial snap covers a fresh run, ResetRun covers a
            // restart. An aborted ceremony never reaches Transitioning, so it never fires a stray sweep.
            _levelProgress.Phase
                .Where(phase => phase == LevelUpPhase.Transitioning)
                .Subscribe(_ => BeginSweep())
                .AddTo(_subscriptions);

            SnapToLevel();
        }

        void ITickable.Tick()
        {
            if (!_sweeping)
            {
                return;
            }

            _sweepElapsed += Time.unscaledDeltaTime;
            var duration = _settings.SweepDuration;
            var t = duration > 0f ? Mathf.Clamp01(_sweepElapsed / duration) : 1f;
            SetAngle(Mathf.Lerp(_sweepFromAngle, _sweepToAngle, Ease(t)));

            if (t >= 1f)
            {
                _sweeping = false;
            }
        }

        public void ResetRun(int generation)
        {
            if (!_settings.NightModeEnabled)
            {
                return;
            }

            SnapToLevel();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void BeginSweep()
        {
            _sweepFromAngle = _currentAngle;
            _sweepToAngle = AngleForLevel(_levelProgress.Level.Value);
            _sweepElapsed = 0f;
            _sweeping = true;
        }

        private void SnapToLevel()
        {
            _sweeping = false;
            SetAngle(AngleForLevel(_levelProgress.Level.Value));
        }

        private void SetAngle(float degrees)
        {
            _currentAngle = degrees;
            _service.SetDirection(VectorMathExtensions.DirectionFromAngle(degrees * Mathf.Deg2Rad));
        }

        private float Ease(float t)
        {
            var curve = _settings.SweepEase;
            return curve != null && curve.length > 0 ? curve.Evaluate(t) : t;
        }

        // Level 1 = the authored rest direction's angle; each further level adds DegreesPerLevel.
        // Continuous (never wrapped) so lerping toward it is always-forward through the wrap.
        private float AngleForLevel(int level)
        {
            var baseAngle = _lightSettings.LightDirection.Angle01() * 360f;
            return baseAngle + (level - 1) * _settings.DegreesPerLevel;
        }
    }
}
