using System;
using BalloonParty.Configuration;
using BalloonParty.Display;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using Navigation = BalloonParty.Shared.GameState.Navigation;

namespace BalloonParty.Game.Cinematics
{
    internal class LevelUpTrailEffect : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Inject] private CinematicDirector _director;
        [Inject] private ICinematicsSettings _cinematicsSettings;
        [Inject] private IGameConfiguration _config;
        [Inject] private ISubscriber<ScorePointMessage> _scoredSubscriber;
        [Inject] private ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        [Inject] private ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        [Inject] private OrthogonalSizeCameraController _orthoController;
        [Inject] private ScoreController _scoreController;
        [Inject] private ScoreTrailService _scoreTrailService;
        [Inject] private PauseService _pauseService;

        private CameraRigCinematicSettings _settings;
        private CinematicCameraRig _cameraRig;
        private Vector3 _lastTrailPosition;
        private float _realElapsed;
        private bool _sessionActive;
        private IDisposable _sessionSubscription;
        private Tween _timeScaleTween;
        private TrailId _tippingTrailId;
        private TrailFlight _trackedFlight;
        private float _trailElapsed;
        private Vector3 _trailOrigin;
        private Vector3 _trailTargetWorld;

        private void Awake()
        {
            // Injection precedes Awake here: GameLifetimeScope's execution order (-5001) builds the
            // container before any other component wakes.
            _settings = _cinematicsSettings.LevelUp;
            _cameraRig = new CinematicCameraRig(
                _camera, _orthoController, _settings.ZoomAmount, _settings.PanWeight, _settings.FollowSpeed);
        }

        private void Start()
        {
            _scoredSubscriber.Subscribe(OnScorePoint).AddTo(this);
        }

        private void OnDestroy()
        {
            DisposeSessionSubscription();
            KillTweens();

            if (_director.IsCinematicActive)
            {
                _director.EndCinematic();
            }

            if (_pauseService.IsPaused(PauseSource.Cinematic))
            {
                _pauseService.Resume(PauseSource.Cinematic);
            }

            _cameraRig?.EnableOrtho(true);

            Time.timeScale = 1f;
        }

        private void KillTweens()
        {
            _timeScaleTween?.Kill();
            _timeScaleTween = null;
            _cameraRig?.KillTween();
        }

        private void OnScorePoint(ScorePointMessage msg)
        {
            if (_sessionActive || Cinematic.IsPlaying)
            {
                return;
            }

            if (!_scoreController.WillLevelUp())
            {
                return;
            }

            _sessionActive = true;
            _tippingTrailId = new TrailId(msg);
            _trackedFlight = null;
            _lastTrailPosition = msg.WorldPosition;

            WaitForTippingTrailAsync().Forget();
        }

        private async UniTaskVoid WaitForTippingTrailAsync()
        {
            var cts = this.GetCancellationTokenOnDestroy();

            await UniTask.WaitUntil(
                () => _scoreTrailService.Flights.Contains(_tippingTrailId),
                cancellationToken: cts);

            if (!_sessionActive)
            {
                return;
            }

            _trackedFlight = _scoreTrailService.Flights.Get(_tippingTrailId);
            if (_trackedFlight == null)
            {
                return;
            }

            BeginCinematicWithTrail();
        }

        private void BeginCinematicWithTrail()
        {
            _trailElapsed = 0f;
            _realElapsed = 0f;
            _trailOrigin = _trackedFlight.Transform.position;
            _lastTrailPosition = _trailOrigin;

            _trailTargetWorld = _scoreTrailService.GetTarget(_tippingTrailId.Color).Center;

            _director.BeginCinematic(CinematicState.LevelUpPanIn);
            _pauseService.Pause(PauseSource.Cinematic);

            _trackedFlight.Transform.GetComponent<FlyingTrail>().DisableMoveTween();
            _trackedFlight.Pause();

            SubscribeForPanIn();

            PreparePanIn();
            _director.PlayScene(new CinematicScene(onTick: PanInTick));
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_director.IsScenePlaying)
            {
                return;
            }

            var matches = msg.ColorName == _tippingTrailId.Color
                          && msg.Score == _tippingTrailId.Score
                          && msg.Level == _tippingTrailId.Level;

            if (!matches)
            {
                return;
            }

            EndPanIn();
        }

        private void OnDismissed()
        {
            DisposeSessionSubscription();
            _director.BeginCinematic(CinematicState.LevelUpRestore);
            _pauseService.Resume(PauseSource.Cinematic);
            PrepareRestore();
            _director.PlayScene(new CinematicScene(onEnd: OnRestoreComplete));
        }

        private void OnRestoreComplete()
        {
            Time.timeScale = 1f;
            _cameraRig.Restore();
            _sessionActive = false;
            _director.EndCinematic();
            Navigation.TransitionTo(NavigationState.Game);
        }

        private void PanInTick()
        {
            if (!_cameraRig.HasCamera)
            {
                return;
            }

            var dt = Time.unscaledDeltaTime;
            _realElapsed += dt;

            var curveT = Mathf.Clamp01(_realElapsed / _settings.SlowDownCurve.Duration());
            var speedFactor = _settings.SlowDownCurve.Evaluate(curveT);
            _trailElapsed += dt * speedFactor;

            if (AdvanceTrackedTrail())
            {
                return;
            }

            _cameraRig.FollowTrail(_lastTrailPosition, dt);
        }

        // Moves the tracked trail toward its target. Returns true once it has arrived (so the
        // caller skips the camera follow this tick), having ended the pan-in if still playing.
        private bool AdvanceTrackedTrail()
        {
            if (_trackedFlight?.Transform == null)
            {
                return false;
            }

            var progress = Mathf.Clamp01(_trailElapsed / _config.ScorePointTraceDuration);

            _trackedFlight.Transform.localScale = Vector3.one * _cinematicsSettings.LevelUpTrackedTrailScaleCurve.Evaluate(progress);

            var target = _trailTargetWorld;
            target.z = 0f;
            _trackedFlight.Transform.position = Vector3.Lerp(_trailOrigin, target, progress);
            _lastTrailPosition = _trackedFlight.Transform.position;

            if (progress < 1f)
            {
                return false;
            }

            _trackedFlight.Complete();
            if (_director.IsScenePlaying)
            {
                EndPanIn();
            }

            return true;
        }

        private void PreparePanIn()
        {
            _timeScaleTween?.Kill();
            _timeScaleTween = null;
            _cameraRig.PreparePanIn(_settings.SlowDownCurve.Duration());
        }

        private void PrepareRestore()
        {
            KillTweens();

            var restoreDuration = _settings.RestoreCurve.Duration();
            var elapsed = 0f;

            _timeScaleTween = DOTween.To(
                    () => elapsed,
                    x =>
                    {
                        elapsed = x;
                        Time.timeScale = _settings.RestoreCurve.Evaluate(x);
                    },
                    restoreDuration,
                    restoreDuration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .OnComplete(() => _director.CompleteScene());

            if (_cameraRig.HasCamera)
            {
                _cameraRig.PrepareRestore(restoreDuration);
            }
            else
            {
                _director.CompleteScene();
            }
        }

        private void SubscribeForPanIn()
        {
            DisposeSessionSubscription();
            _sessionSubscription = _trailArrivedSubscriber.Subscribe(OnTrailArrived);
        }

        private void SubscribeForDismissed()
        {
            DisposeSessionSubscription();
            _sessionSubscription = _dismissedSubscriber.Subscribe(_ => OnDismissed());
        }

        private void DisposeSessionSubscription()
        {
            _sessionSubscription?.Dispose();
            _sessionSubscription = null;
        }

        private void EndPanIn()
        {
            DisposeSessionSubscription();
            _trackedFlight = null;
            KillTweens();
            _scoreTrailService.Flights.CompleteAll();
            _director.CompleteScene();
            _director.EndCinematic();
            SubscribeForDismissed();
        }
    }
}
