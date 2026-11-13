using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Game.Upgrades;
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
        private readonly IPublisher<ThrowerReadyMessage> _readyPublisher;
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
        private readonly IWeaponStats _weapon;
        private readonly PauseService _pauseService;
        private readonly IObjectResolver _resolver;
        private readonly List<Vector3> _tracePoints = new();
        private readonly PoolManager _poolManager;
        private readonly ThrowerSettings _settings;
        private readonly ThrowerView _view;
        private readonly IProjectileRegistryWriter _registry;
        private readonly ThrowerOriginProvider _originProvider;
        private readonly PredictionTraceProvider _traceProvider;
        private readonly SceneLightFieldService _lightField;
        private readonly IGamePalette _palette;
        private readonly CompositeDisposable _subscriptions = new();

        // Every shot that has left the thrower and not yet died, oldest first. The registry mirrors
        // these for readers; the thrower keeps the views because only it pools them.
        private readonly List<ProjectileView> _inFlight = new();

        // Reused so killing every shot (whose deaths edit _inFlight) never iterates the live list.
        private readonly List<ProjectileView> _killBuffer = new();

        // Cached since Object.name allocates; Reload() hits this twice per shot.
        private readonly string _projectilePoolKey;

        // Stores the quantized direction each frame (what RotateTo/the prediction trace actually show
        // the player), so a latched fire reproduces exactly the heading that was on screen a moment
        // earlier rather than re-deriving it from a raw sample.
        private readonly AimDirectionHistory _aimHistory = new(AimHistoryCapacity);
        private readonly ThrowerReload _reload;

        // The ball held in the thrower, aiming. The next one loads the moment a shot leaves, so this is
        // null only around level transitions, game over, and before the entrance finishes.
        private IWriteableProjectileModel _loadedProjectile;
        private ProjectileView _loadedView;
        private Vector3 _direction = Vector3.up;
        private bool _isMovable;
        private bool _tracePublished;
        private bool _wasReady;

        // The cap the pool was last warmed for; an Extra Pen card raises the cap mid-run.
        private int _warmedCap;
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
            IPublisher<ThrowerReadyMessage> readyPublisher,
            ISubscriber<RunResetMessage> resetSubscriber,
            ISubscriber<RunRestartCompletedMessage> restartCompletedSubscriber,
            ISubscriber<BoardClearMessage> boardClearSubscriber,
            ISubscriber<ForceDestroyProjectileMessage> forceDestroySubscriber,
            ISubscriber<LevelTransitionCompletedMessage> levelTransitionCompletedSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ISubscriber<LevelUpAbandonedMessage> levelUpAbandonedSubscriber,
            ILevelProgress levelProgress,
            IWeaponStats weapon,
            PauseService pauseService,
            IProjectileRegistryWriter registry,
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
            _readyPublisher = readyPublisher;
            _resetSubscriber = resetSubscriber;
            _restartCompletedSubscriber = restartCompletedSubscriber;
            _boardClearSubscriber = boardClearSubscriber;
            _forceDestroySubscriber = forceDestroySubscriber;
            _levelTransitionCompletedSubscriber = levelTransitionCompletedSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _levelUpAbandonedSubscriber = levelUpAbandonedSubscriber;
            _levelProgress = levelProgress;
            _weapon = weapon;
            _pauseService = pauseService;
            _registry = registry;
            _originProvider = originProvider;
            _traceProvider = traceProvider;
            _lightField = lightField;
            _palette = palette;
            _projectilePoolKey = settings.ProjectilePrefab.name;
            _reload = new ThrowerReload(weapon);
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
                _view.transform,
                ContactRadius.FromCollider(
                    projectilePrefab != null ? projectilePrefab.GetComponent<Collider2D>() : null,
                    projectilePrefab != null ? projectilePrefab.transform.lossyScale.x : 1f));

            _poolManager.Register(_projectilePoolKey,
                new ProjectilePoolChannel(_resolver, _settings.ProjectilePrefab));

            // Every ball the cap allows in flight, the one held, and as many again still playing their
            // disappear: a dead ball returns to the pool only once that ends, so a burst of deaths
            // would otherwise Instantiate mid-play.
            _warmedCap = _weapon.MaxBallsInFlight;
            _poolManager.Prewarm(_projectilePoolKey, _warmedCap * 2 + 1);

            // The spent shot scales away. The thrower normally already holds the next ball (it loads on
            // fire); this only reloads an empty thrower, and never during a level-up, whose new ball
            // arrives with the new level (LevelTransitionCompleted).
            _destroyedSubscriber.Subscribe(HandleProjectileDestroyed).AddTo(_subscriptions);

            // Completing was abandoned (run ending/restarting), so the skipped reload happens now or the
            // thrower stays empty forever.
            _levelUpAbandonedSubscriber.Subscribe(_ => LoadProjectile()).AddTo(_subscriptions);

            // Force-destroy: the level controller commands every shot to die through its canonical
            // death path (board depleted, or the CompletingCap timed out).
            _forceDestroySubscriber.Subscribe(_ => ForceDestroyProjectiles()).AddTo(_subscriptions);
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
            _gameOverSubscriber.Subscribe(_ => ScaleAwayAllProjectiles()).AddTo(_subscriptions);

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
                || _pauseService.IsAnyPaused.Value)
            {
                // A pause or state change mid-aim must take the trace down with it, or the aim line
                // and hit markers linger through the overflow/level-up/loss windows.
                ClearPredictionTrace();
                return;
            }

            UpdateDirection();
            _view.RotateTo(_direction);
            UpdateLoadedProjectilePosition();
            UpdatePredictionTrace();
            TryFire();
            AnnounceReady();
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
        // [IProjectileFlightConfig.AimAngleMinDegrees, AimAngleMaxDegrees] (via AimAngleGrid), so
        // its angles are already in range by construction; ShotSolverWindow's arc is deliberately
        // user-editable for experimentation and is allowed to probe outside the player's reachable
        // range on purpose (see its README) — clamping here would silently defeat that.
        //
        // Skips the reload (it snaps the ball to the muzzle anyway) but not the ball cap or the level
        // phase, so a tool can never put more balls in flight than play allows, nor fire into a level-up.
        internal void FireAt(Vector3 direction)
        {
            if (!_isMovable || _loadedProjectile == null || _loadedView == null
                || !_reload.CanLaunch(_inFlight.Count, _levelProgress.Phase.Value))
            {
                return;
            }

            _direction = direction.normalized;
            _view.RotateTo(_direction);
            _loadedView.transform.position = _view.SpawnPointPosition;
            _loadedView.transform.rotation = _view.Rotation;
            Fire();
        }

        // Clamps a raw aim direction into [minAngleDegrees, maxAngleDegrees] and, for a quantized aim
        // (stepDegrees > 0), snaps it to the nearest multiple of stepDegrees — so the pointer keeps
        // moving continuously while the heading it produces jumps between fixed headings within the
        // range. A game rule, so it lives here rather than in ThrowerView, which only reports raw
        // pointer state. The range is never opt-in (unlike the step) — it always applies, even with
        // stepDegrees <= 0 (a plain clamp).
        //
        // Order matters, and AimAngleGrid.ClampToReachableAngle is why this delegates rather than
        // clamping-then-snapping or snapping-then-clamping here: clamp first and a snap can push the
        // result back outside the range; snap first and a clamp can land on an angle that isn't on the
        // grid. ClampToReachableAngle sidesteps both by clamping the ROUNDED GRID INDEX, guaranteeing
        // the result is always both a step multiple and inside the range — and it's the exact same
        // grid AimAngleGrid's sweep helpers sample, so the player's reachable angles and a sweep's
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
            var resolved = AimAngleGrid.ClampToReachableAngle(angle, minAngleDegrees, maxAngleDegrees, stepDegrees);
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

        // Loads only into an empty thrower: fire and death must never throw away the ball being aimed.
        private void EnsureLoaded()
        {
            if (_loadedView != null || !_isMovable || _levelProgress.Phase.Value != LevelUpPhase.Playing)
            {
                return;
            }

            LoadProjectile();
        }

        // A fresh ball (and a fresh ProjectileLoadedMessage) for a new level, a restart or the entrance.
        // The ball still held here carried the old level's state, so it is pooled rather than kept.
        // Loads run after the Ascent and after a reset, which is where a new card takes effect, so a raised
        // cap Instantiates its extra balls there rather than mid-play.
        private void WarmPoolForCap()
        {
            var cap = _weapon.MaxBallsInFlight;
            if (cap <= _warmedCap)
            {
                return;
            }

            _poolManager.Prewarm(_projectilePoolKey, (cap - _warmedCap) * 2);
            _warmedCap = cap;
        }

        private void LoadProjectile()
        {
            WarmPoolForCap();

            if (_loadedView != null)
            {
                _registry.Retire(_loadedProjectile);
                _poolManager.Return(_projectilePoolKey, _loadedView);
                ClearLoaded();
            }

            _loadedView = _poolManager.Get<ProjectileView>(_projectilePoolKey);
            _loadedView.transform.position = _view.Position;
            _loadedView.transform.rotation = _view.Rotation;

            _loadedProjectile = new ProjectileModel
            {
                Speed = _flightConfig.ProjectileSpeed,
                IsFree = false,
                Direction = _direction
            };
            _loadedProjectile.ShieldsRemaining.Value = _weapon.StartingShields;

            // A held shot isn't stepping yet, so nothing has resolved a flight speed for it — seed the one
            // feedback reads (the shield field's ripple) with base speed rather than leaving it at zero.
            _loadedProjectile.Flight.CurrentSpeed = _loadedProjectile.Speed;

            _loadedView.Bind(_loadedProjectile);
            _registry.Load(_loadedProjectile, _loadedView.transform);
            _loadedPublisher.Publish(new ProjectileLoadedMessage(_loadedProjectile));
            _reload.Restart();
        }

        // A shot fired in the level-up frame never took a step: it goes back in the thrower as the held
        // shot. Any other unflown shot (the thrower already holds one) is pooled outright.
        private void UnfireIfNeverFlown()
        {
            for (var i = _inFlight.Count - 1; i >= 0; i--)
            {
                var view = _inFlight[i];
                if (view.HasFlown)
                {
                    continue;
                }

                _inFlight.RemoveAt(i);
                if (_loadedView == null)
                {
                    view.Model.IsFree = false;
                    _loadedView = view;
                    _loadedProjectile = view.Model;
                    _registry.Unlaunch(view.Model);
                }
                else
                {
                    _registry.Retire(view.Model);
                    _poolManager.Return(_projectilePoolKey, view);
                }
            }
        }

        // Scales the spent shot away (it returns to the pool only once its disappear finishes) and loads a
        // fresh instance now, so Get() never hands back one still mid-disappear.
        private void HandleProjectileDestroyed(ProjectileDestroyedMessage message)
        {
            var index = IndexOfInFlight(message.Projectile);
            if (index >= 0)
            {
                var view = _inFlight[index];
                _inFlight.RemoveAt(index);
                ScaleAway(view);
            }
            else if (_loadedView != null && message.Projectile == _loadedProjectile)
            {
                // Force-destroying an empty-handed thrower kills the held shot (see ForceDestroyProjectiles).
                var view = _loadedView;
                ClearLoaded();
                ScaleAway(view);
            }

            EnsureLoaded();
        }

        // Force-destroys through each shot's canonical death path (pierce discharge, state cleanup,
        // ProjectileDestroyedMessage), whose DestroyedMessage then scales it away and pools it. With
        // nothing in flight it kills the held shot instead, as the single-shot thrower always did.
        private void ForceDestroyProjectiles()
        {
            if (_inFlight.Count == 0)
            {
                if (_loadedView != null)
                {
                    _loadedView.ForceDestroy();
                }

                return;
            }

            _killBuffer.Clear();
            _killBuffer.AddRange(_inFlight);
            foreach (var view in _killBuffer)
            {
                view.ForceDestroy();
            }

            _killBuffer.Clear();
        }

        // Scales every shot away and pools it, without loading a replacement (game-over reloads only on
        // restart). No Destroyed is published, so Clear retires them all.
        private void ScaleAwayAllProjectiles()
        {
            foreach (var view in _inFlight)
            {
                ScaleAway(view);
            }

            _inFlight.Clear();

            if (_loadedView != null)
            {
                var view = _loadedView;
                ClearLoaded();
                ScaleAway(view);
            }

            _registry.Clear();
        }

        // Lands only: on the Destroyed path the dying view retires its own ball once that dispatch ends.
        private void ScaleAway(ProjectileView view)
        {
            _registry.Land(view.Model);
            view.PlayDisappear(() => _poolManager.Return(_projectilePoolKey, view));
        }

        private void ClearLoaded()
        {
            _loadedProjectile = null;
            _loadedView = null;
        }

        private int IndexOfInFlight(IProjectileModel projectile)
        {
            for (var i = 0; i < _inFlight.Count; i++)
            {
                if (_inFlight[i].Model == projectile)
                {
                    return i;
                }
            }

            return -1;
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
            _registry.Clear();

            foreach (var view in _inFlight)
            {
                _poolManager.Return(_projectilePoolKey, view);
            }

            _inFlight.Clear();

            if (_loadedView != null)
            {
                _poolManager.Return(_projectilePoolKey, _loadedView);
            }

            ClearLoaded();
            LoadProjectile();
        }

        // A release with no ball ready fires nothing and is not queued: the held ball's place in the
        // thrower already told the player it wasn't ready.
        private void TryFire()
        {
            if (!_view.FireReleased || !IsReadyToFire())
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
            _loadedProjectile.IsFree = true;
            _loadedProjectile.Direction = _direction;
            _registry.Launch(_loadedProjectile);
            _inFlight.Add(_loadedView);
            ClearLoaded();
            ClearPredictionTrace();
            _view.PlayRecoil(_direction);
            // A fast follow-up shot must not latch onto a direction recorded before this one fired.
            _aimHistory.Clear();
            EnsureLoaded();
        }

        internal bool IsReadyToFire()
        {
            return _loadedProjectile != null
                   && _loadedView != null
                   && _reload.IsReady(_inFlight.Count, _levelProgress.Phase.Value);
        }

        // Once per ball, on the tick it becomes ready: the cue that a release now fires. After TryFire,
        // so a ball fired the tick it got ready doesn't cue a release that already happened.
        private void AnnounceReady()
        {
            var isReady = IsReadyToFire();
            if (isReady && !_wasReady)
            {
                _readyPublisher.Publish(new ThrowerReadyMessage(_loadedProjectile, _reload.TakeWaitedAtCap()));
            }

            _wasReady = isReady;
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
            if (_loadedProjectile == null || _loadedView == null)
            {
                return;
            }

            _reload.Advance(Time.deltaTime, _inFlight.Count);
            _loadedView.transform.position =
                Vector3.Lerp(_view.Position, _view.SpawnPointPosition, _reload.SlidePosition);
            _loadedView.transform.rotation = _view.Rotation;
            _loadedProjectile.Direction = _direction;
        }

        private void UpdatePredictionTrace()
        {
            if (_loadedProjectile == null || !_view.IsAiming)
            {
                ClearPredictionTrace();
                return;
            }

            // From the contact circle, not the transform: the collider leads the origin, so a trace
            // started at the transform begins behind where the shot really is.
            _traceCalculator.Calculate(
                _loadedView.ContactCenter, _direction, _loadedView.ContactRadius, _tracePoints,
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
