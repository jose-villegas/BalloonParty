using System;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
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
using Navigation = BalloonParty.Shared.GameState.Navigation;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The level-up cinematic, as a plain C# producer over the shared <see cref="CameraRigCinematic"/>
    ///     runner in its split-phase form: when a score point will tip the level, the tipping trail is
    ///     intercepted and puppeted along the pan-in segment's curve (gameplay paused — the curve
    ///     modulates the trail, not timeScale) while the camera follows it; the pan-in ends on the
    ///     trail's arrival, the popup gate opens, and the dismissed message starts the restore phase
    ///     (timeScale sampled from its curve out of the popup's frozen 0), which hands play back.
    /// </summary>
    internal sealed class LevelUpCinematic : IStartable, IDisposable
    {
        private readonly CinematicDirector _director;
        private readonly CinematicCameraRig _rig;
        private readonly TimeScaleService _timeScale;
        private readonly ICinematicsSettings _settings;
        private readonly IGameConfiguration _config;
        private readonly ISubscriber<ScorePointMessage> _scoredSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly IScoreQuery _scoreQuery;
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
            IScoreQuery scoreQuery,
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
            _scoreQuery = scoreQuery;
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

            // No level-up show on a run that is over or already certain to be — the loss wins.
            if (Navigation.Current.Value != NavigationState.Game || _lossForecast.LossImminent)
            {
                return;
            }

            if (!_scoreQuery.WillLevelUp())
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
            await UniTask.WaitUntil(
                () => _scoreTrailService.Flights.Contains(_tippingTrailId),
                cancellationToken: _cts.Token);

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
            _trailOrigin = _trackedFlight.Transform.position;
            _lastTrailPosition = _trailOrigin;
            _trailTargetWorld = _scoreTrailService.GetTarget(_tippingTrailId.Color).Center;

            // Re-check after the async trail wait: the loss may have committed (or become certain)
            // in the frames between the tipping pop and the trail registering.
            if (Navigation.Current.Value != NavigationState.Game || _lossForecast.LossImminent
                || !_cinematic.TryBegin())
            {
                // The run ended, its loss is certain, or another cinematic won the race — let this
                // level-up resolve without the show.
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
            _pauseService.Resume(PauseSource.Cinematic);
            _cinematic.TryBeginRestore();
        }

        private void OnCinematicEnded()
        {
            _sessionActive = false;
            Navigation.TransitionTo(NavigationState.Game);
        }

        // Extra pan-in work each tick: the segment's curve value modulates the tipping trail's playback
        // speed (gameplay is paused, so timeScale stays untouched); ending the pan-in inside the hook
        // makes the runner skip the camera follow for the tick, as before.
        private void PanInTick(float dt, float curveValue)
        {
            // The loss can become certain mid-pan-in (the same turn's spawn keeps rejecting while the
            // projectile is frozen). The loss wins: drop the show so the heart-drain and game-over
            // present unobstructed.
            if (_lossForecast.LossImminent)
            {
                AbortSession();
                return;
            }

            _trailElapsed += dt * curveValue;
            AdvanceTrackedTrail();
        }

        // Moves the tracked trail toward its target, ending the pan-in once it has arrived.
        private void AdvanceTrackedTrail()
        {
            if (_trackedFlight?.Transform == null)
            {
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

        // Drops the whole level-up show (pan-in or the popup limbo between phases): trails complete,
        // gameplay resumes, camera and time snap back to base. Used when the loss overtakes the ceremony.
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
