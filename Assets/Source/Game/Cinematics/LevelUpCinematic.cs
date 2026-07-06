using System;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Cinematics;
using Navigation = BalloonParty.Shared.GameState.Navigation;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Puppets the level-up's tipping score trail along the pan-in, then hands off to the popup and restore phases.
    /// </summary>
    internal sealed class LevelUpCinematic : IStartable, IDisposable
    {
        // Timeout multiples of the trail's flight duration, so a lost/mismatched trail can't soft-lock the popup.
        private const float PanInTimeoutFactor = 3f;
        private const float TrailRegisterTimeoutFactor = 3f;

        private readonly CinematicDirector _director;
        private readonly CinematicCameraRig _rig;
        private readonly TimeScaleService _timeScale;
        private readonly ICinematicsSettings _settings;
        private readonly IGameConfiguration _config;
        private readonly ISubscriber<ScorePointMessage> _scoredSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly ILevelProgress _levelProgress;
        private readonly ILossForecast _lossForecast;
        private readonly ScoreTrailService _scoreTrailService;
        private readonly PauseService _pauseService;
        private readonly CancellationTokenSource _cts = new();

        private CameraRigCinematic _cinematic;
        private TrackedTrailSettings _trackedTrailSettings;
        private IDisposable _scoreSubscription;
        private IDisposable _sessionSubscription;
        private Vector3 _lastTrailPosition;
        private bool _sessionActive;
        private TrailId _tippingTrailId;
        private TrailFlight _trackedFlight;
        private float _trailElapsed;
        private float _panInElapsed;
        private Vector3 _trailOrigin;
        private Vector3 _trailTargetWorld;

        [Inject]
        internal LevelUpCinematic(
            CinematicDirector director,
            CinematicCameraRig rig,
            TimeScaleService timeScale,
            ICinematicsSettings settings,
            IGameConfiguration config,
            ISubscriber<ScorePointMessage> scoredSubscriber,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber,
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            ILevelProgress levelProgress,
            ILossForecast lossForecast,
            ScoreTrailService scoreTrailService,
            PauseService pauseService)
        {
            _director = director;
            _rig = rig;
            _timeScale = timeScale;
            _settings = settings;
            _config = config;
            _scoredSubscriber = scoredSubscriber;
            _dismissedSubscriber = dismissedSubscriber;
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _levelProgress = levelProgress;
            _lossForecast = lossForecast;
            _scoreTrailService = scoreTrailService;
            _pauseService = pauseService;
        }

        public void Start()
        {
            _trackedTrailSettings = _settings.EntryOf(CinematicState.LevelUpPanIn).TrackedTrail;
            _cinematic = new CameraRigCinematic(_director, _rig, _timeScale, _settings, new CameraRigCinematicConfig
            {
                PanInState = CinematicState.LevelUpPanIn,
                RestoreState = CinematicState.LevelUpRestore,
                Focus = new PointFocus(() => _lastTrailPosition),
                DrivesTimeScale = false,
                RestoreEvaluatesCurve = true,
                OnPanInTick = PanInTick,
                OnEnded = OnCinematicEnded,
            });

            _scoreSubscription = _scoredSubscriber.Subscribe(OnScorePoint);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            DisposeSessionSubscription();
            _scoreSubscription?.Dispose();
            _cinematic?.Abort();

            if (_pauseService.IsPaused(PauseSource.Cinematic))
            {
                _pauseService.Resume(PauseSource.Cinematic);
            }
        }

        private void OnScorePoint(ScorePointMessage msg)
        {
            if (_sessionActive || Cinematic.IsPlaying)
            {
                return;
            }

            // No level-up show on a run that is over or already certain to be lost.
            if (Navigation.Current.Value != NavigationState.Game || _lossForecast.LossImminent)
            {
                return;
            }

            if (!_levelProgress.WillLevelUp())
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
            // Bounded wait: if the tipping trail never registers, give up instead of waiting forever.
            var elapsed = 0f;
            var timeout = _config.ScorePointTraceDuration * TrailRegisterTimeoutFactor;
            while (!_scoreTrailService.Flights.Contains(_tippingTrailId))
            {
                if (elapsed >= timeout)
                {
                    _sessionActive = false;
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
                elapsed += Time.unscaledDeltaTime;
            }

            if (!_sessionActive)
            {
                return;
            }

            _trackedFlight = _scoreTrailService.Flights.Get(_tippingTrailId);
            if (_trackedFlight == null)
            {
                _sessionActive = false;
                return;
            }

            BeginCinematicWithTrail();
        }

        private void BeginCinematicWithTrail()
        {
            _trailElapsed = 0f;
            _panInElapsed = 0f;
            _trailOrigin = _trackedFlight.Transform.position;
            _lastTrailPosition = _trailOrigin;
            _trailTargetWorld = _scoreTrailService.GetTarget(_tippingTrailId.Color).Center;

            // Re-check after the async trail wait: the loss may have committed since the tipping pop.
            if (Navigation.Current.Value != NavigationState.Game || _lossForecast.LossImminent
                || !_cinematic.TryBegin())
            {
                _sessionActive = false;
                return;
            }

            _pauseService.Pause(PauseSource.Cinematic);

            _trackedFlight.Transform.GetComponent<FlyingTrail>().DisableMoveTween();
            _trackedFlight.Pause();

            SubscribeForPanIn();
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_cinematic.IsPanInRunning)
            {
                return;
            }

            var matches = msg.ColorName == _tippingTrailId.Color
                          && msg.Score == _tippingTrailId.Score;

            if (!matches)
            {
                return;
            }

            EndPanIn();
        }

        // Camera un-zoom is driven by LevelTransitionController, not this producer.
        private void OnDismissed()
        {
            DisposeSessionSubscription();
            _pauseService.Resume(PauseSource.Cinematic);
            OnCinematicEnded();
        }

        private void OnCinematicEnded()
        {
            _sessionActive = false;
            Navigation.TransitionTo(NavigationState.Game);
        }

        // Curve value modulates the tipping trail's playback speed; timeScale stays untouched during pan-in.
        private void PanInTick(float dt, float curveValue)
        {
            // Loss can become certain mid-pan-in; it wins, so drop the show.
            if (_lossForecast.LossImminent)
            {
                AbortSession();
                return;
            }

            // Absolute safety cap: end the pan-in so the popup's gate opens rather than soft-locking.
            _panInElapsed += dt;
            if (_panInElapsed > _config.ScorePointTraceDuration * PanInTimeoutFactor)
            {
                EndPanIn();
                return;
            }

            _trailElapsed += dt * curveValue;
            AdvanceTrackedTrail();
        }

        private void AdvanceTrackedTrail()
        {
            // Trail was completed/returned out from under us — end the pan-in instead of stalling.
            if (_trackedFlight?.Transform == null)
            {
                if (_cinematic.IsPanInRunning)
                {
                    EndPanIn();
                }

                return;
            }

            var progress = Mathf.Clamp01(_trailElapsed / _config.ScorePointTraceDuration);

            _trackedFlight.Transform.localScale = Vector3.one * _trackedTrailSettings.ScaleCurve.Evaluate(progress);

            var target = _trailTargetWorld;
            target.z = 0f;
            _trackedFlight.Transform.position = Vector3.Lerp(_trailOrigin, target, progress);
            _lastTrailPosition = _trackedFlight.Transform.position;

            if (progress < 1f)
            {
                return;
            }

            _trackedFlight.Complete();
            if (_cinematic.IsPanInRunning)
            {
                EndPanIn();
            }
        }

        // Drops the whole level-up show when the loss overtakes the ceremony.
        private void AbortSession()
        {
            DisposeSessionSubscription();
            _trackedFlight = null;
            _scoreTrailService.Flights.CompleteAll();

            if (_pauseService.IsPaused(PauseSource.Cinematic))
            {
                _pauseService.Resume(PauseSource.Cinematic);
            }

            _cinematic.Abort();
            _sessionActive = false;
        }

        private void EndPanIn()
        {
            DisposeSessionSubscription();
            _trackedFlight = null;
            _scoreTrailService.Flights.CompleteAll();
            _cinematic.EndPanIn();
            SubscribeForDismissed();
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
    }
}
