using System;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Game.Score.Behaviours;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Cinematics;
using Navigation = BalloonParty.Shared.GameState.Navigation;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Puppets the level-up's tipping score trail along the pan-in, then hands off to the popup and restore phases.
    /// </summary>
    internal sealed class LevelUpCinematic : CameraRigCinematicProducer
    {
        // Timeout multiples of the trail's flight duration, so a lost/mismatched trail can't soft-lock the popup.
        private const float PanInTimeoutFactor = 3f;
        private const float TrailRegisterTimeoutFactor = 3f;

        private readonly IGameConfiguration _config;
        private readonly ISubscriber<ScorePointsGroupMessage> _scoredSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly ILevelProgress _levelProgress;
        private readonly ILossForecast _lossForecast;
        private readonly ScoreTrailService _scoreTrailService;
        private readonly ScoreTrailBehaviourResolver _resolver;
        private readonly PauseService _pauseService;
        private readonly CancellationTokenSource _cts = new();

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
            ISubscriber<ScorePointsGroupMessage> scoredSubscriber,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber,
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            ILevelProgress levelProgress,
            ILossForecast lossForecast,
            ScoreTrailService scoreTrailService,
            ScoreTrailBehaviourResolver resolver,
            PauseService pauseService)
            : base(director, rig, timeScale, settings)
        {
            _config = config;
            _scoredSubscriber = scoredSubscriber;
            _dismissedSubscriber = dismissedSubscriber;
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _levelProgress = levelProgress;
            _lossForecast = lossForecast;
            _scoreTrailService = scoreTrailService;
            _resolver = resolver;
            _pauseService = pauseService;
        }

        protected override CameraRigCinematicConfig BuildConfig()
        {
            return new CameraRigCinematicConfig
            {
                PanInState = CinematicState.LevelUpPanIn,
                RestoreState = CinematicState.LevelUpRestore,
                Focus = new PointFocus(() => _lastTrailPosition),
                DrivesTimeScale = false,
                RestoreEvaluatesCurve = true,
                OnPanInTick = PanInTick,
                OnEnded = OnCinematicEnded,
            };
        }

        protected override void OnStart()
        {
            _trackedTrailSettings = Settings.EntryOf(CinematicState.LevelUpPanIn).TrackedTrail;
            _scoreSubscription = _scoredSubscriber.Subscribe(OnScorePoint);
        }

        protected override void OnDispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            DisposeSessionSubscription();
            _scoreSubscription?.Dispose();

            if (_pauseService.IsPaused(PauseSource.Cinematic))
            {
                _pauseService.Resume(PauseSource.Cinematic);
            }
        }

        private void OnScorePoint(ScorePointsGroupMessage msg)
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
            // The handler nominates its principal trail (via the resolver), so the tipping id can never
            // diverge from what actually registers: DefaultScore's FIRST trail spawns immediately and is
            // timeout-safe under the bounded registry wait; the group's LAST trail can spawn seconds later
            // under scatter stagger and would race WaitForTippingTrailAsync's timeout.
            _tippingTrailId = _resolver.PrincipalIdFor(msg);
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
                || !Runner.TryBegin())
            {
                _sessionActive = false;
                return;
            }

            _pauseService.Pause(PauseSource.Cinematic);

            // Formation principals are bare anchor Transforms with no FlyingTrail — only tween-driven trails
            // (DefaultScore) need their move tween killed before we puppet the transform along the pan-in.
            _trackedFlight.Transform.GetComponent<FlyingTrail>()?.DisableMoveTween();
            _trackedFlight.Pause();

            SubscribeForPanIn();
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!Runner.IsPanInRunning)
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
            // Trail was completed/returned out from under us (destroyed transform, or its arrival fired
            // via its own tween / a CompleteAll and the flight went Idle) — the pooled instance may
            // already be flying for another group, so stop steering it and end the pan-in instead.
            if (_trackedFlight?.Transform == null || _trackedFlight.Phase == FlightPhase.Idle)
            {
                _trackedFlight = null;
                if (Runner.IsPanInRunning)
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
            if (Runner.IsPanInRunning)
            {
                EndPanIn();
            }
        }

        private void AbortSession()
        {
            DisposeSessionSubscription();
            _trackedFlight = null;
            _scoreTrailService.Flights.CompleteAll();

            if (_pauseService.IsPaused(PauseSource.Cinematic))
            {
                _pauseService.Resume(PauseSource.Cinematic);
            }

            Runner.Abort();
            _sessionActive = false;
        }

        private void EndPanIn()
        {
            DisposeSessionSubscription();
            _trackedFlight = null;
            _scoreTrailService.Flights.CompleteAll();
            Runner.EndPanIn();
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
            LifecycleHelper.DisposeAndClear(ref _sessionSubscription);
        }
    }
}
