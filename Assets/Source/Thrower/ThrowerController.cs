using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Prediction;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Solver;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Thrower
{
    internal class ThrowerController : IStartable, ITickable, IDisposable
    {
        // Sub-hundredth-of-a-degree noise floor: a direction whose length has decayed this far below unit
        // (mouse resting exactly on the thrower) is not a real heading to snap, only Atan2 noise.
        private const float DegenerateDirectionSqrMagnitude = 1e-8f;

        // At a pessimistic 10 fps (a hitch, not steady state) this holds 3.2s of aim history — far
        // beyond even a generous multiple of the latch's useful range (60-120ms) — for a few hundred
        // bytes. Comfortably covers the largest sensible latch at the lowest sensible frame rate.
        private const int AimHistoryCapacity = 32;

        private readonly IPredictionTraceConfig _traceConfig;
        private readonly IDeflectorField _deflectorField;
        private readonly IPierceItemField _pierceItemField;
        private readonly IProjectileFlightConfig _flightConfig;
        private readonly IPublisher<ProjectileLoadedMessage> _loadedPublisher;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<RunResetMessage> _resetSubscriber;
        private readonly ISubscriber<RunRestartCompletedMessage> _restartCompletedSubscriber;
        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly ISubscriber<ForceDestroyProjectileMessage> _forceDestroySubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _levelTransitionCompletedSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ISubscriber<LevelUpAbandonedMessage> _levelUpAbandonedSubscriber;
        private readonly ILevelProgress _levelProgress;
        private readonly PauseService _pauseService;
        private readonly HoldSpeedUpController _holdSpeedUp;
        private readonly IObjectResolver _resolver;
        private readonly List<Vector3> _tracePoints = new();
        private readonly PoolManager _poolManager;
        private readonly ThrowerSettings _settings;
        private readonly ThrowerView _view;
        private readonly ProjectilePositionProvider _positionProvider;
        private readonly ThrowerOriginProvider _originProvider;
        private readonly PredictionTraceProvider _traceProvider;
        private readonly SceneLightFieldService _lightField;
        private readonly IGamePalette _palette;
        private readonly CompositeDisposable _subscriptions = new();

        // Cached since Object.name allocates; Reload() hits this twice per shot.
        private readonly string _projectilePoolKey;

        // Stores the quantized direction each frame (what RotateTo/the prediction trace actually show
        // the player), so a latched fire reproduces exactly the heading that was on screen a moment
        // earlier rather than re-deriving it from a raw sample.
        private readonly AimDirectionHistory _aimHistory = new(AimHistoryCapacity);

        private IWriteableProjectileModel _activeProjectile;
        private ProjectileView _activeView;
        private Vector3 _direction = Vector3.up;
        private bool _isMovable;
        private bool _tracePublished;
        private float _loadElapsed;
        private float _loadDuration;
        private PredictionTraceCalculator _traceCalculator;
        private PredictionTraceLights _traceLights;

        // Set by FireBestShotCheat (auto-fire toggle) to override the aimed direction on the next
        // player-initiated fire. Consumed (cleared) once used. Null means no override. Not range-clamped
        // here — see FireAt, which both this and the cheat's one-shot path route through.
        internal Vector3? DirectionOverride { get; set; }

        [Inject]
        internal ThrowerController(
            ThrowerView view,
            IPredictionTraceConfig traceConfig,
            IProjectileFlightConfig flightConfig,
            IDeflectorField deflectorField,
            IPierceItemField pierceItemField,
            PoolManager poolManager,
            IObjectResolver resolver,
            ThrowerSettings settings,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            IPublisher<ProjectileLoadedMessage> loadedPublisher,
            ISubscriber<RunResetMessage> resetSubscriber,
            ISubscriber<RunRestartCompletedMessage> restartCompletedSubscriber,
            ISubscriber<BoardClearMessage> boardClearSubscriber,
            ISubscriber<ForceDestroyProjectileMessage> forceDestroySubscriber,
            ISubscriber<LevelTransitionCompletedMessage> levelTransitionCompletedSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ISubscriber<LevelUpAbandonedMessage> levelUpAbandonedSubscriber,
            ILevelProgress levelProgress,
            PauseService pauseService,
            HoldSpeedUpController holdSpeedUp,
            ProjectilePositionProvider positionProvider,
            ThrowerOriginProvider originProvider,
            PredictionTraceProvider traceProvider,
            SceneLightFieldService lightField,
            IGamePalette palette)
        {
            _view = view;
            _traceConfig = traceConfig;
            _flightConfig = flightConfig;
            _deflectorField = deflectorField;
            _pierceItemField = pierceItemField;
            _poolManager = poolManager;
            _resolver = resolver;
            _settings = settings;
            _destroyedSubscriber = destroyedSubscriber;
            _loadedPublisher = loadedPublisher;
            _resetSubscriber = resetSubscriber;
            _restartCompletedSubscriber = restartCompletedSubscriber;
            _boardClearSubscriber = boardClearSubscriber;
            _forceDestroySubscriber = forceDestroySubscriber;
            _levelTransitionCompletedSubscriber = levelTransitionCompletedSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _levelUpAbandonedSubscriber = levelUpAbandonedSubscriber;
            _levelProgress = levelProgress;
            _pauseService = pauseService;
            _holdSpeedUp = holdSpeedUp;
            _positionProvider = positionProvider;
            _originProvider = originProvider;
            _traceProvider = traceProvider;
            _lightField = lightField;
            _palette = palette;
            _projectilePoolKey = settings.ProjectilePrefab.name;
        }

        public void Start()
        {
            _traceCalculator = new PredictionTraceCalculator(
                _traceConfig, _flightConfig, _deflectorField, _pierceItemField);
            // Off by default (IPredictionTraceConfig.LightingEnabled) — a prior attempt at this made the
            // actors the line crosses read as noise, kept togglable rather than deleted. Constructed
            // unconditionally since every call is a no-op while the toggle is off.
            _traceLights = new PredictionTraceLights(
                _lightField, _traceConfig, _palette.PaletteIndexOf(GamePalette.PredictionColorId));
            _view.SetTraceColor(_traceConfig.LineColor);
            // Off the prefab, not a spawned view: nothing has been fired yet, and ProjectileView
            // resolves its own radius in Awake, which a prefab asset never runs.
            var projectilePrefab = _settings.ProjectilePrefab;
            _originProvider.Set(
                _view.SpawnPoint,
                ContactRadius.FromCollider(
                    projectilePrefab != null ? projectilePrefab.GetComponent<Collider2D>() : null,
                    projectilePrefab != null ? projectilePrefab.transform.lossyScale.x : 1f));

            _poolManager.Register(_projectilePoolKey,
                new ProjectilePoolChannel(_resolver, _settings.ProjectilePrefab));

            _poolManager.Prewarm(_projectilePoolKey, 2);

            // The spent shot scales away while the fresh one loads — unless a level-up claimed the
            // window, in which case the new shot arrives with the new level (LevelTransitionCompleted).
            _destroyedSubscriber.Subscribe(_ => HandleProjectileDestroyed()).AddTo(_subscriptions);

            // Completing was abandoned (run ending/restarting), so the skipped reload happens now or the
            // thrower stays empty forever.
            _levelUpAbandonedSubscriber.Subscribe(_ => LoadProjectile()).AddTo(_subscriptions);

            // Force-destroy: the level controller commands the projectile to die through its canonical
            // death path (board depleted, or the CompletingCap timed out).
            _forceDestroySubscriber.Subscribe(_ => ForceDestroyActiveProjectile()).AddTo(_subscriptions);
            _levelTransitionCompletedSubscriber.Subscribe(_ => LoadProjectile()).AddTo(_subscriptions);

            // A shot fired in the very frame the level-up triggers never takes a physics step before the
            // freeze — un-fire it, or the dismissal swap scale-drifts it from the muzzle like a phantom.
            _levelUpSubscriber.Subscribe(_ => UnfireIfNeverFlown()).AddTo(_subscriptions);

            // An immediate restart reloads now; a cinematic-deferred one (the loss→restart camera-down)
            // reloads on RunRestartCompletedMessage instead, so the shot arrives with the settled board.
            _resetSubscriber.Subscribe(OnRunReset).AddTo(_subscriptions);
            _restartCompletedSubscriber.Subscribe(_ => LoadProjectile()).AddTo(_subscriptions);
            _boardClearSubscriber.Subscribe(_ => Reload()).AddTo(_subscriptions);

            // A projectile fired just before loss keeps flying on physics alone; scale it away.
            _gameOverSubscriber.Subscribe(_ => ScaleAwayActiveProjectile()).AddTo(_subscriptions);

            Navigation.Current
                .Where(state => state == NavigationState.Game)
                .Take(1)
                .Subscribe(_ => PlayEntrance())
                .AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _traceLights?.Dispose();
            _subscriptions.Dispose();
        }

        public void Tick()
        {
            if (!_isMovable
                || Navigation.Current.Value != NavigationState.Game
                || _pauseService.IsAnyPaused.Value
                || _holdSpeedUp.ConsumedInput)
            {
                // A pause or state change mid-aim must take the trace down with it, or the aim line
                // and hit markers linger through the overflow/level-up/loss windows.
                // ConsumedInput: the hold that sped up the previous flight hasn't been released yet —
                // suppress aiming so the player must tap fresh to aim the next shot.
                ClearPredictionTrace();
                return;
            }

            UpdateDirection();
            _view.RotateTo(_direction);
            UpdateLoadedProjectilePosition();
            UpdatePredictionTrace();
            TryFire();
        }

        // Editor-tooling entry (Shot Solver window / Fire Best Shot cheat): aim the thrower at the
        // given direction and fire, bypassing mouse input. Snaps the loaded shot to the spawn point
        // first so the launch origin matches the solver's per-angle simulation exactly. Deliberately
        // NOT pause-gated — the cheat console holds PauseSource.Cheat while open, so a pause guard
        // would make the cheat a silent no-op; arming while paused is safe because the pause-gated
        // FixedUpdate keeps the shot still until the menu closes.
        //
        // Deliberately does not run the direction through ClampAndQuantizeAimDirection — same as it
        // already bypasses quantization. FireBestShotCheat sweeps within
        // [IProjectileFlightConfig.AimAngleMinDegrees, AimAngleMaxDegrees] (via ShotBoardGather), so
        // its angles are already in range by construction; ShotSolverWindow's arc is deliberately
        // user-editable for experimentation and is allowed to probe outside the player's reachable
        // range on purpose (see its README) — clamping here would silently defeat that.
        internal void FireAt(Vector3 direction)
        {
            if (!_isMovable || _activeProjectile == null || _activeView == null || _activeProjectile.IsFree)
            {
                return;
            }

            _direction = direction.normalized;
            _view.RotateTo(_direction);
            _activeView.transform.position = _view.SpawnPointPosition;
            _activeView.transform.rotation = _view.Rotation;
            Fire();
        }

        // Clamps a raw aim direction into [minAngleDegrees, maxAngleDegrees] and, for a quantized aim
        // (stepDegrees > 0), snaps it to the nearest multiple of stepDegrees — so the pointer keeps
        // moving continuously while the heading it produces jumps between fixed headings within the
        // range. A game rule, so it lives here rather than in ThrowerView, which only reports raw
        // pointer state. The range is never opt-in (unlike the step) — it always applies, even with
        // stepDegrees <= 0 (a plain clamp).
        //
        // Order matters, and ShotBoardGather.ClampToReachableAngle is why this delegates rather than
        // clamping-then-snapping or snapping-then-clamping here: clamp first and a snap can push the
        // result back outside the range; snap first and a clamp can land on an angle that isn't on the
        // grid. ClampToReachableAngle sidesteps both by clamping the ROUNDED GRID INDEX, guaranteeing
        // the result is always both a step multiple and inside the range — and it's the exact same
        // grid ShotBoardGather's sweep samples, so the player's reachable angles and the solver's
        // swept ones can never disagree.
        //
        // A degenerate (near-zero) direction passes through unchanged — there is no angle to clamp.
        // internal static so the pure operation is edit-mode testable without spinning up the controller.
        internal static Vector3 ClampAndQuantizeAimDirection(
            Vector3 direction, float stepDegrees, float minAngleDegrees, float maxAngleDegrees)
        {
            if (direction.sqrMagnitude < DegenerateDirectionSqrMagnitude)
            {
                return direction;
            }

            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var resolved = ShotBoardGather.ClampToReachableAngle(angle, minAngleDegrees, maxAngleDegrees, stepDegrees);
            var radians = resolved * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        }

        private void PlayEntrance()
        {
            _view.AnimateEntrance().OnComplete(() =>
            {
                _isMovable = true;
                LoadProjectile();
            });
        }

        private void LoadProjectile()
        {
            _activeView = _poolManager.Get<ProjectileView>(_projectilePoolKey);
            _activeView.transform.position = _view.Position;
            _activeView.transform.rotation = _view.Rotation;

            _activeProjectile = new ProjectileModel
            {
                Speed = _flightConfig.ProjectileSpeed,
                IsFree = false,
                Direction = _direction
            };
            _activeProjectile.ShieldsRemaining.Value = _flightConfig.ProjectileStartingShields;

            // A held shot isn't stepping yet, so nothing has resolved a flight speed for it — seed the one
            // feedback reads (the shield field's ripple) with base speed rather than leaving it at zero.
            _activeProjectile.Flight.CurrentSpeed = _activeProjectile.Speed;

            _activeView.Bind(_activeProjectile);
            _positionProvider.Set(_activeView.transform);
            _loadedPublisher.Publish(new ProjectileLoadedMessage(_activeProjectile));

            _loadElapsed = 0f;
            _loadDuration = _flightConfig.ProjectileLoadDuration;
        }

        private void UnfireIfNeverFlown()
        {
            if (_activeProjectile == null || _activeView == null
                || !_activeProjectile.IsFree || _activeView.HasFlown)
            {
                return;
            }

            _activeProjectile.IsFree = false;
            _positionProvider.SetFree(false);
        }

        // Scales the spent shot away (it returns to the pool only once its disappear finishes) and loads a
        // fresh instance now, so Get() never hands back one still mid-disappear.
        private void HandleProjectileDestroyed()
        {
            ScaleAwayActiveProjectile();

            if (_levelProgress.Phase.Value == LevelUpPhase.Playing)
            {
                LoadProjectile();
            }
        }

        // Force-destroys the projectile through its canonical death path (pierce discharge, state
        // cleanup, ProjectileDestroyedMessage). The subsequent DestroyedMessage triggers
        // HandleProjectileDestroyed which handles the visual cleanup and pool return.
        private void ForceDestroyActiveProjectile()
        {
            if (_activeView == null)
            {
                return;
            }

            _activeView.ForceDestroy();
        }

        // Scales the shot away and pools it, without loading a replacement (game-over reloads only on restart).
        private void ScaleAwayActiveProjectile()
        {
            if (_activeView == null)
            {
                return;
            }

            var view = _activeView;
            _positionProvider.Clear();
            _activeProjectile = null;
            _activeView = null;
            view.PlayDisappear(() => _poolManager.Return(_projectilePoolKey, view));
        }

        private void OnRunReset(RunResetMessage message)
        {
            // Only an immediate restart reloads here; a cinematic-deferred board waits for
            // RunRestartCompletedMessage so the shot lands with the settled board, not mid-transition.
            if (message.BoardReset)
            {
                Reload();
            }
        }

        private void Reload()
        {
            // Tick is gated during resets, so a trace left active at reset time would strand a stale marker.
            ClearPredictionTrace();
            _positionProvider.Clear();

            if (_activeView != null)
            {
                _poolManager.Return(_projectilePoolKey, _activeView);
            }

            _activeProjectile = null;
            _activeView = null;
            LoadProjectile();
        }

        private void TryFire()
        {
            if (_activeProjectile == null || _activeView == null || _activeProjectile.IsFree)
            {
                return;
            }

            if (!_view.FireReleased)
            {
                return;
            }

            if (DirectionOverride.HasValue)
            {
                _direction = DirectionOverride.Value;
                DirectionOverride = null;
            }
            else
            {
                _direction = ResolveLatchedDirection();
            }

            Fire();
        }

        // Trusts the aim from AimLatchSeconds before now, not the live sample: the release instant is
        // the least trustworthy one in a touch gesture (see AimDirectionHistory). 0 (the default) skips
        // the lookup and fires the live direction — today's exact behaviour.
        private Vector3 ResolveLatchedDirection()
        {
            var latchSeconds = _flightConfig.AimLatchSeconds;
            if (latchSeconds <= 0f)
            {
                return _direction;
            }

            return _aimHistory.TryResolve(Time.time - latchSeconds, out var direction) ? direction : _direction;
        }

        private void Fire()
        {
            _activeProjectile.IsFree = true;
            _activeProjectile.Direction = _direction;
            _positionProvider.SetFree(true);
            ClearPredictionTrace();
            _view.PlayRecoil(_direction);
            // A fast follow-up shot must not latch onto a direction recorded before this one fired.
            _aimHistory.Clear();
        }

        private void UpdateDirection()
        {
            if (!_view.IsAiming)
            {
                return;
            }

            if (_view.TryGetAimDirection(out var direction))
            {
                _direction = ClampAndQuantizeAimDirection(
                    direction, _flightConfig.AimAngleStepDegrees, _flightConfig.AimAngleMinDegrees,
                    _flightConfig.AimAngleMaxDegrees);
                _aimHistory.Record(Time.time, _direction);
            }
        }

        private void UpdateLoadedProjectilePosition()
        {
            if (_activeProjectile == null || _activeView == null || _activeProjectile.IsFree)
            {
                return;
            }

            if (_loadElapsed < _loadDuration)
            {
                _loadElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(_loadElapsed / _loadDuration);
                var eased = DOVirtual.EasedValue(0f, 1f, t, Ease.OutBack);
                _activeView.transform.position = Vector3.Lerp(_view.Position, _view.SpawnPointPosition, eased);
            }
            else
            {
                _activeView.transform.position = _view.SpawnPointPosition;
            }

            _activeView.transform.rotation = _view.Rotation;
            _activeProjectile.Direction = _direction;
        }

        private void UpdatePredictionTrace()
        {
            if (_activeProjectile == null || _activeProjectile.IsFree || !_view.IsAiming)
            {
                ClearPredictionTrace();
                return;
            }

            // From the contact circle, not the transform: the collider leads the origin, so a trace
            // started at the transform begins behind where the shot really is.
            _traceCalculator.Calculate(
                _activeView.ContactCenter, _direction, _activeView.ContactRadius, _tracePoints,
                out var traceEnd);
            _view.SetTrace(_tracePoints);
            _traceProvider.SetTrace(_tracePoints, traceEnd);
            _traceLights.SetTrace(_tracePoints);
            _tracePublished = true;
        }

        // Idempotent so the every-tick not-aiming path doesn't re-issue LineRenderer clears and
        // provider version bumps.
        private void ClearPredictionTrace()
        {
            if (!_tracePublished)
            {
                return;
            }

            _view.ClearTrace();
            _traceProvider.Clear();
            _traceLights.Clear();
            _tracePublished = false;
        }
    }
}
