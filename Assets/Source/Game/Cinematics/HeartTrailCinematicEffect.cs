using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Display;
using BalloonParty.Game.Health;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using Navigation = BalloonParty.Shared.GameState.Navigation;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The overflow heart-drain cinematic: starts when the first heart trail flies from the UI to an
    ///     overflow pop, eases the game into slow-motion (fast → slowest as the hearts reach the balloons)
    ///     and pans/zooms the camera to follow the centroid of all in-flight heart trails. Ends when the
    ///     overflow has fully drained or the run is over (extra pops after 0 HP don't extend it).
    ///
    ///     Uses <see cref="CinematicState.HeartDrain"/>, which is neither loss-blocking (the 0-HP
    ///     game-over fires through <c>RunController.EndRun</c> even while this plays) nor shake-blocking
    ///     (each heart launch punches the camera through the pan; the follow lerp absorbs it).
    /// </summary>
    internal class HeartTrailCinematicEffect : MonoBehaviour
    {
        [Header("Slow Motion")]
        [SerializeField] private AnimationCurve _slowDownCurve = AnimationCurve.EaseInOut(0f, 1f, 0.6f, 0.3f);
        [SerializeField] private float _restoreSeconds = 0.4f;

        [Header("Camera")]
        [SerializeField] private Camera _camera;
        [SerializeField] private float _zoomAmount = 0.5f;
        [SerializeField] private float _panWeight = 0.7f;
        [SerializeField] private float _followSpeed = 5f;

        [Inject] private CinematicDirector _director;
        [Inject] private OrthogonalSizeCameraController _orthoController;
        [Inject] private HeartTrailTracker _tracker;
        [Inject] private RejectedBalloonEffect _overflow;
        [Inject] private ISubscriber<OverflowHeartRequestedMessage> _heartRequestedSubscriber;

        private readonly List<Vector3> _trailPositions = new();

        private CinematicCameraRig _rig;
        private IDisposable _subscription;
        private Tween _timeScaleTween;
        private bool _active;
        private float _realElapsed;

        private void Awake()
        {
            _rig = new CinematicCameraRig(_camera, _orthoController, _zoomAmount, _panWeight, _followSpeed);
        }

        private void Start()
        {
            _subscription = _heartRequestedSubscriber.Subscribe(_ => OnFirstHeart());
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            KillTimeScaleTween();

            if (_active && _director.IsCinematicActive)
            {
                _director.EndCinematic();
            }

            _rig?.EnableOrtho(true);
            Time.timeScale = 1f;
        }

        private void OnFirstHeart()
        {
            // Begin when the first heart of an overflow burst launches — not while already running or
            // during another cinematic, and only in active play.
            if (_active || Cinematic.IsPlaying || Navigation.Current.Value != NavigationState.Game)
            {
                return;
            }

            _active = true;
            _realElapsed = 0f;
            KillTimeScaleTween();

            _director.BeginCinematic(CinematicState.HeartDrain);
            _rig.PreparePanIn(_slowDownCurve.Duration());
            _director.PlayScene(new CinematicScene(onTick: DrainTick));
        }

        private void DrainTick()
        {
            var dt = Time.unscaledDeltaTime;
            _realElapsed += dt;

            var curveT = Mathf.Clamp01(_realElapsed / _slowDownCurve.Duration());
            Time.timeScale = _slowDownCurve.Evaluate(curveT);

            if (_rig.HasCamera)
            {
                _trailPositions.Clear();
                var active = _tracker.Active;
                for (var i = 0; i < active.Count; i++)
                {
                    if (active[i] != null)
                    {
                        _trailPositions.Add(active[i].position);
                    }
                }

                _rig.FollowPoints(_trailPositions, _trailPositions.Count, dt);
            }

            if (ShouldEnd())
            {
                BeginRestore();
            }
        }

        // Ends when the run is over (game-over already fired at 0 HP — later pops don't count) or the
        // pile has fully drained: the overflow hold released and no heart trails remain in flight.
        private bool ShouldEnd()
        {
            return Navigation.Current.Value == NavigationState.GameOver
                   || (!_overflow.IsOverflowActive && _tracker.Active.Count == 0);
        }

        private void BeginRestore()
        {
            KillTimeScaleTween();

            _timeScaleTween = DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 1f, _restoreSeconds)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true)
                .OnComplete(() => _director.CompleteScene());

            if (_rig.HasCamera)
            {
                _rig.PrepareRestore(_restoreSeconds);
            }

            _director.PlayScene(new CinematicScene(onEnd: OnRestoreComplete));
        }

        private void OnRestoreComplete()
        {
            Time.timeScale = 1f;
            _rig.Restore();
            _active = false;
            _director.EndCinematic();
        }

        private void KillTimeScaleTween()
        {
            _timeScaleTween?.Kill();
            _timeScaleTween = null;
        }
    }
}
