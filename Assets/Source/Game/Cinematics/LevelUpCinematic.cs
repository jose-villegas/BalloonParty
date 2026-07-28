using System;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Follows the tipping projectile during Window A (claim → first wall), then hands off to the popup and restore.
    /// </summary>
    internal sealed class LevelUpCinematic : CameraRigCinematicProducer
    {

        private readonly ISubscriber<ScorePointsGroupMessage> _scoredSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly IPublisher<LevelUpAbortedMessage> _abortedPublisher;
        private readonly ILevelProgress _levelProgress;
        private readonly ILossForecast _lossForecast;
        private readonly ScoreTrailService _scoreTrailService;
        private readonly ProjectilePositionProvider _positionProvider;
        private readonly PauseService _pauseService;
        private readonly Rect _outerBounds;

        private IDisposable _scoreSubscription;
        private IDisposable _sessionSubscription;
        private IDisposable _freezeSubscription;
        private Vector3 _lastProjectilePosition;
        private bool _sessionActive;
        private float _panInElapsed;

        [Inject]
        internal LevelUpCinematic(
            CinematicDirector director,
            CinematicCameraRig rig,
            TimeScaleService timeScale,
            ICinematicsSettings settings,
            ISubscriber<ScorePointsGroupMessage> scoredSubscriber,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber,
            IPublisher<LevelUpAbortedMessage> abortedPublisher,
            ILevelProgress levelProgress,
            ILossForecast lossForecast,
            ScoreTrailService scoreTrailService,
            ProjectilePositionProvider positionProvider,
            PauseService pauseService,
            IProjectileFlightConfig flightConfig)
            : base(director, rig, timeScale, settings)
        {
            _scoredSubscriber = scoredSubscriber;
            _dismissedSubscriber = dismissedSubscriber;
            _abortedPublisher = abortedPublisher;
            _levelProgress = levelProgress;
            _lossForecast = lossForecast;
            _scoreTrailService = scoreTrailService;
            _positionProvider = positionProvider;
            _pauseService = pauseService;

            var limits = new WallLimits(flightConfig.LimitsClockwise);
            var offset = settings.EntryOf(CinematicState.LevelCompleteRestore).Rig.ZoomAmount;
            _outerBounds = new Rect(
                limits.Left - offset,
                limits.Bottom - offset,
                limits.Right - limits.Left + offset * 2f,
                limits.Top - limits.Bottom + offset * 2f);
        }

        protected override CameraRigCinematicConfig BuildConfig()
        {
            return new CameraRigCinematicConfig
            {
                PanInState = CinematicState.LevelCompleteHit,
                RestoreState = CinematicState.LevelCompleteRestore,
                Focus = new PointFocus(GetCameraTarget),
                DrivesTimeScale = false,
                RestoreEvaluatesCurve = true,
                OnPanInTick = PanInTick,
                OnEnded = OnCinematicEnded,
            };
        }

        protected override void OnStart()
        {
            _scoreSubscription = _scoredSubscriber.Subscribe(OnScorePoint);
        }

        protected override void OnDispose()
        {
            DisposeSessionSubscription();
            LifecycleHelper.DisposeAndClear(ref _freezeSubscription);
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

            if (_levelProgress.Phase.Value != LevelUpPhase.Completing)
            {
                return;
            }

            _sessionActive = true;
            _lastProjectilePosition = _positionProvider.IsActive
                ? _positionProvider.Position
                : msg.WorldPosition;

            BeginCinematic();
        }

        private void BeginCinematic()
        {
            _panInElapsed = 0f;

            if (!Runner.TryBegin())
            {
                _sessionActive = false;
                return;
            }

            // Freeze airborne trails when the popup appears (Phase → Pending) so shapes hold behind it
            // instead of snapping away. The level transition resolves them as outgoing-level content.
            LifecycleHelper.DisposeAndClear(ref _freezeSubscription);
            _freezeSubscription = _levelProgress.Phase
                .Where(phase => phase == LevelUpPhase.Pending)
                .Subscribe(_ => _scoreTrailService.Flights.PauseAll());

            SubscribeForDismissed();
        }

        // Camera un-zoom is driven by LevelTransitionController, not this producer.
        private void OnDismissed()
        {
            DisposeSessionSubscription();
            OnCinematicEnded();
        }

        private void OnCinematicEnded()
        {
            LifecycleHelper.DisposeAndClear(ref _freezeSubscription);
            _sessionActive = false;
        }

        private void PanInTick(float dt, float curveValue)
        {
            if (_lossForecast.LossImminent)
            {
                AbortSession();
                return;
            }

            _panInElapsed += dt;

            // Pan-in runs for the full LevelCompleteHit curve duration, then rolls into restore.
            var panInDuration = Settings.EntryOf(CinematicState.LevelCompleteHit).Rig.TimeScaleCurve.Duration();
            if (_panInElapsed >= panInDuration)
            {
                EndPanIn();
            }
        }

        private void AbortSession()
        {
            DisposeSessionSubscription();
            LifecycleHelper.DisposeAndClear(ref _freezeSubscription);
            _scoreTrailService.Flights.CompleteAll();

            if (_pauseService.IsPaused(PauseSource.Cinematic))
            {
                _pauseService.Resume(PauseSource.Cinematic);
            }

            Runner.Abort();
            _sessionActive = false;
            _abortedPublisher.Publish(new LevelUpAbortedMessage());
        }

        private void EndPanIn()
        {
            Runner.EndPanIn();

            // Pan-out: slow zoom back to base size while still following the projectile,
            // box-in-box clamped so the frustum never leaves the outer bounds.
            var restoreSegment = Settings.EntryOf(CinematicState.LevelCompleteRestore).Rig;
            Rig.RestoreCurveDriven(restoreSegment.TimeScaleCurve, GetCameraTarget, restoreSegment.FollowSpeed, _outerBounds);
        }

        private Vector3 GetCameraTarget()
        {
            if (_positionProvider.IsActive)
            {
                _lastProjectilePosition = _positionProvider.Position;
            }

            return _lastProjectilePosition;
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
