using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pause;
using DG.Tweening;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The reusable camera-rig cinematic shape: a pan-in segment (optional timeScale drive + camera
    ///     framing of an <see cref="ICinematicFocus"/> + an optional per-tick hook) followed by a restore
    ///     segment (timeScale back to 1, camera back to base). Runs continuously when the config has an
    ///     end condition, or as producer-driven split phases (<see cref="EndPanIn"/> …
    ///     <see cref="TryBeginRestore"/>) around an external gate like the level-up popup. Segments and
    ///     durations come from the states' <see cref="CameraRigCinematicSettings"/>. Owns its begin/end
    ///     pairing and teardown (<see cref="Abort"/>), so producers stop carrying repair code.
    /// </summary>
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

        /// <summary>
        ///     Producer-driven end of a split pan-in: stops tweens and ends the cinematic while leaving
        ///     the camera where it is (the rig keeps its captured base for the later restore). No-op
        ///     unless the pan-in is running.
        /// </summary>
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

        /// <summary>
        ///     Hard teardown for owner disposal: kills tweens, restores time and camera, ends the
        ///     cinematic if it is still this runner's.
        /// </summary>
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

            // Snap camera and framing back to base (also re-enables the ortho controller) — an abort
            // can happen mid-flight in gameplay, not just on teardown.
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

            // The hook may end the pan-in (a tracked trail arriving) — don't frame or roll on after it.
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

        // The continuous form: the restore continues the active cinematic (state switch, no gate).
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
                // Sample the curve absolutely — the level-up ramps from the popup's frozen 0, where
                // "from current" would be meaningless.
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
                // Tween from the CURRENT timeScale, so an early end (e.g. game-over during the pan-in
                // ramp) doesn't snap speed down before ramping back up.
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
