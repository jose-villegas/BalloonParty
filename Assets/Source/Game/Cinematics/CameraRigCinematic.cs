using System;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The reusable camera-rig cinematic shape: a pan-in segment (per-tick timeScale drive + camera
    ///     framing of an <see cref="ICinematicFocus"/>) that runs until an end condition, then a restore
    ///     segment (timeScale eased back to 1 from wherever it currently is + camera returned to base).
    ///     A producer is just this runner plus a trigger, a focus and an end condition — segments and
    ///     durations come from the states' <see cref="CameraRigCinematicSettings"/>. Owns its
    ///     begin/end pairing and teardown (<see cref="Abort"/>), so producers stop carrying repair code.
    /// </summary>
    internal sealed class CameraRigCinematic
    {
        private readonly CinematicDirector _director;
        private readonly CinematicCameraRig _rig;
        private readonly CinematicState _panInState;
        private readonly CinematicState _restoreState;
        private readonly CameraRigCinematicSettings _panInSegment;
        private readonly CameraRigCinematicSettings _restoreSegment;
        private readonly ICinematicFocus _focus;
        private readonly Func<bool> _endCondition;

        private Tween _timeScaleTween;
        private float _realElapsed;
        private bool _running;

        public CameraRigCinematic(
            CinematicDirector director,
            CinematicCameraRig rig,
            ICinematicsSettings settings,
            CinematicState panInState,
            CinematicState restoreState,
            ICinematicFocus focus,
            Func<bool> endCondition)
        {
            _director = director;
            _rig = rig;
            _panInState = panInState;
            _restoreState = restoreState;
            _panInSegment = settings.EntryOf(panInState).Rig;
            _restoreSegment = settings.EntryOf(restoreState).Rig;
            _focus = focus;
            _endCondition = endCondition;
        }

        public bool IsRunning => _running;

        /// <summary>Begins the cinematic unless another one is active (drop policy in the director).</summary>
        public bool TryBegin()
        {
            if (_running || !_director.TryBeginCinematic(_panInState))
            {
                return false;
            }

            _running = true;
            _realElapsed = 0f;
            KillTimeScaleTween();

            _rig.PreparePanIn(_panInSegment);
            _director.PlayScene(new CinematicScene(onTick: PanInTick));
            return true;
        }

        /// <summary>
        ///     Hard teardown for owner disposal: kills tweens, restores time and camera, ends the
        ///     cinematic if it is still this runner's.
        /// </summary>
        public void Abort()
        {
            KillTimeScaleTween();

            if (!_running)
            {
                return;
            }

            _running = false;

            if (_director.IsCinematicActive)
            {
                _director.EndCinematic();
            }

            _rig.EnableOrtho(true);
            Time.timeScale = 1f;
        }

        private void PanInTick()
        {
            var dt = Time.unscaledDeltaTime;
            _realElapsed += dt;

            var curve = _panInSegment.TimeScaleCurve;
            Time.timeScale = curve.Evaluate(Mathf.Clamp01(_realElapsed / curve.Duration()));

            _rig.Frame(_focus, _panInSegment, dt);

            if (_endCondition())
            {
                BeginRestore();
            }
        }

        private void BeginRestore()
        {
            KillTimeScaleTween();
            _director.BeginCinematic(_restoreState);

            // Tween from the CURRENT timeScale rather than sampling the restore curve, so an early end
            // (e.g. game-over during the pan-in ramp) doesn't snap speed down before ramping back up.
            var restoreSeconds = _restoreSegment.TimeScaleCurve.Duration();
            _timeScaleTween = DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 1f, restoreSeconds)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true)
                .OnComplete(() => _director.CompleteScene());

            if (_rig.HasCamera)
            {
                _rig.PrepareRestore(restoreSeconds);
            }

            _director.PlayScene(new CinematicScene(onEnd: OnRestoreComplete));
        }

        private void OnRestoreComplete()
        {
            Time.timeScale = 1f;
            _rig.Restore();
            _running = false;
            _director.EndCinematic();
        }

        private void KillTimeScaleTween()
        {
            _timeScaleTween?.Kill();
            _timeScaleTween = null;
        }
    }
}
