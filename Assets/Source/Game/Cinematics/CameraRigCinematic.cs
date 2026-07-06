using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pause;
using DG.Tweening;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>Owns its begin/end pairing and teardown so producers stop carrying repair code.</summary>
    internal sealed class CameraRigCinematic
    {
        private readonly CinematicDirector _director;
        private readonly CinematicCameraRig _rig;
        private readonly TimeScaleService _timeScale;
        private readonly CameraRigCinematicConfig _config;
        private readonly CameraRigCinematicSettings _panInSegment;
        private readonly CameraRigCinematicSettings _restoreSegment;

        private Tween _timeScaleTween;
        private float _realElapsed;
        private bool _panInRunning;
        private bool _restoreRunning;

        public CameraRigCinematic(
            CinematicDirector director,
            CinematicCameraRig rig,
            TimeScaleService timeScale,
            ICinematicsSettings settings,
            CameraRigCinematicConfig config)
        {
            _director = director;
            _rig = rig;
            _timeScale = timeScale;
            _config = config;
            _panInSegment = settings.EntryOf(config.PanInState).Rig;
            _restoreSegment = settings.EntryOf(config.RestoreState).Rig;
        }

        public bool IsPanInRunning => _panInRunning;
        public bool IsRunning => _panInRunning || _restoreRunning;

        /// <summary>Begins the pan-in unless another cinematic is active (drop policy in the director).</summary>
        public bool TryBegin()
        {
            if (IsRunning || !_director.TryBeginCinematic(_config.PanInState))
            {
                return false;
            }

            _panInRunning = true;
            _realElapsed = 0f;
            KillTimeScaleTween();

            _rig.PreparePanIn(_panInSegment);
            _director.PlayScene(new CinematicScene(onTick: PanInTick));
            return true;
        }

        /// <summary>Ends the cinematic but leaves the camera in place for the later restore; no-op unless running.</summary>
        public void EndPanIn()
        {
            if (!_panInRunning)
            {
                return;
            }

            _panInRunning = false;
            KillTimeScaleTween();
            _timeScale.Release(TimeScaleSource.Cinematic);
            _rig.KillTween();
            _director.CompleteScene();
            _director.EndCinematic();
        }

        /// <summary>Begins a split restore phase as its own cinematic (after an external gate).</summary>
        public bool TryBeginRestore()
        {
            if (IsRunning || !_director.TryBeginCinematic(_config.RestoreState))
            {
                return false;
            }

            StartRestoreSegment();
            return true;
        }

        /// <summary>Hard teardown for owner disposal: kills tweens, restores time and camera.</summary>
        public void Abort()
        {
            KillTimeScaleTween();

            if (!IsRunning)
            {
                return;
            }

            _panInRunning = false;
            _restoreRunning = false;

            if (_director.IsCinematicActive)
            {
                _director.EndCinematic();
            }

            // Abort can happen mid-flight in gameplay, not just on teardown.
            _rig.KillTween();
            _rig.Restore();
            _timeScale.Release(TimeScaleSource.Cinematic);
        }

        private void PanInTick()
        {
            var dt = Time.unscaledDeltaTime;
            _realElapsed += dt;

            var curve = _panInSegment.TimeScaleCurve;
            var curveValue = curve.Evaluate(Mathf.Clamp01(_realElapsed / curve.Duration()));
            if (_config.DrivesTimeScale)
            {
                _timeScale.Claim(TimeScaleSource.Cinematic, curveValue);
            }

            _config.OnPanInTick?.Invoke(dt, curveValue);

            // The hook may end the pan-in — don't frame or roll on after it.
            if (!_panInRunning)
            {
                return;
            }

            _rig.Frame(_config.Focus, _panInSegment, dt);

            if (_config.EndCondition != null && _config.EndCondition())
            {
                RollIntoRestore();
            }
        }

        private void RollIntoRestore()
        {
            _panInRunning = false;
            _director.BeginCinematic(_config.RestoreState);
            StartRestoreSegment();
        }

        private void StartRestoreSegment()
        {
            _restoreRunning = true;
            KillTimeScaleTween();

            var restoreCurve = _restoreSegment.TimeScaleCurve;
            var restoreSeconds = restoreCurve.Duration();

            if (_config.RestoreEvaluatesCurve)
            {
                // Sample absolutely — the level-up ramps from the popup's frozen 0.
                var elapsed = 0f;
                _timeScaleTween = DOTween.To(
                        () => elapsed,
                        x =>
                        {
                            elapsed = x;
                            _timeScale.Claim(TimeScaleSource.Cinematic, restoreCurve.Evaluate(x));
                        },
                        restoreSeconds,
                        restoreSeconds)
                    .SetEase(Ease.Linear)
                    .SetUpdate(true)
                    .OnComplete(() => _director.CompleteScene());
            }
            else
            {
                // Tween from the CURRENT timeScale so an early end doesn't snap speed down first.
                _timeScaleTween = DOTween.To(
                        () => Time.timeScale,
                        x => _timeScale.Claim(TimeScaleSource.Cinematic, x),
                        1f,
                        restoreSeconds)
                    .SetEase(Ease.InOutQuad)
                    .SetUpdate(true)
                    .OnComplete(() => _director.CompleteScene());
            }

            if (_rig.HasCamera)
            {
                _rig.PrepareRestore(restoreSeconds);
            }

            _director.PlayScene(new CinematicScene(onEnd: OnRestoreComplete));
        }

        private void OnRestoreComplete()
        {
            _timeScale.Release(TimeScaleSource.Cinematic);
            _rig.Restore();
            _restoreRunning = false;
            _director.EndCinematic();
            _config.OnEnded?.Invoke();
        }

        private void KillTimeScaleTween()
        {
            _timeScaleTween?.Kill();
            _timeScaleTween = null;
        }
    }
}
