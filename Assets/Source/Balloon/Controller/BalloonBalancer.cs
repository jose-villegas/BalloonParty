using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Projectile.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using UniRx;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Balloon.Controller
{
    internal class BalloonBalancer : IStartable, ITickable, IRunResettable, IDisposable
    {
        // Sub-millimeter total travel (squared world units): below this a move is degenerate — a
        // (near-)zero-length path makes DOTween's ConvertToConstantPathPerc divide 0/0 into NaN positions.
        private const float DegenerateMoveSqrEpsilon = 1e-6f;

        // Higher priority intervenes first; equal priorities keep the sweep's original order.
        private static readonly Comparison<RoamCandidate> ByRoamOrder = (a, b) =>
            a.Priority != b.Priority ? b.Priority - a.Priority : a.Order - b.Order;

        private readonly BalancePathHolder _balancePathHolder;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly SlotGrid _grid;
        private readonly GridBalanceQuery _balanceQuery;
        private readonly ISubscriber<BalanceBalloonsMessage> _subscriber;
        private readonly ISubscriber<ProjectileLoadedMessage> _projectileLoadedSubscriber;
        private readonly ISubscriber<ProjectileDestroyedMessage> _projectileDestroyedSubscriber;
        private readonly PauseService _pauseService;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly BalloonMotionTicker _motionTicker;
        private readonly Action<object> _finalizeBalanceMove;
        private readonly PressurePropagation _pressurePropagation;
        private readonly BalancePlanner _balancePlanner;
        private readonly Dictionary<IWriteableDynamicSlotActor, List<Vector3>> _paths = new();
        private readonly List<PressurePropagation.Move> _pressureMoves = new();
        private readonly List<BalanceMove> _balanceMoves = new();
        private readonly Dictionary<IWriteableDynamicSlotActor, int> _turnSteps = new();
        private readonly List<RoamCandidate> _roamers = new();
        private readonly List<Vector2Int> _restingSlots = new();
        private readonly Dictionary<int, Vector3[]> _waypointBuffers = new();
        private readonly CompositeDisposable _subscriptions = new();

        private bool _balanceRequested;
        private int _generation;
        private IProjectileModel _activeProjectile;
        private float _flightRebalanceElapsed;

        public int ResetOrder => RunResetOrder.Quiesce;

        // Exposed for tests.
        internal int Generation => _generation;

        [Inject]
        internal BalloonBalancer(
            SlotGrid grid,
            GridBalanceQuery balanceQuery,
            IBalloonsConfiguration balloonsConfig,
            BalancePathHolder balancePathHolder,
            ISubscriber<BalanceBalloonsMessage> subscriber,
            ISubscriber<ProjectileLoadedMessage> projectileLoadedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> projectileDestroyedSubscriber,
            PauseService pauseService,
            DisturbanceFieldService disturbanceField,
            BalloonMotionTicker motionTicker,
            BalanceDebugRecorder debugRecorder = null)
        {
            _grid = grid;
            _balanceQuery = balanceQuery;
            _balloonsConfig = balloonsConfig;
            _balancePathHolder = balancePathHolder;
            _subscriber = subscriber;
            _projectileLoadedSubscriber = projectileLoadedSubscriber;
            _projectileDestroyedSubscriber = projectileDestroyedSubscriber;
            _pauseService = pauseService;
            _disturbanceField = disturbanceField;
            _motionTicker = motionTicker;
            _finalizeBalanceMove = FinalizeBalanceMove;
            _pressurePropagation = new PressurePropagation(grid, balanceQuery.Evaluator, debugRecorder);
            _balancePlanner = new BalancePlanner(grid, balanceQuery);
        }

        public void Start()
        {
            _subscriber.Subscribe(_ => RequestBalance()).AddTo(_subscriptions);
            _projectileLoadedSubscriber.Subscribe(msg => _activeProjectile = msg.Model).AddTo(_subscriptions);
            _projectileDestroyedSubscriber.Subscribe(_ => OnProjectileDestroyed()).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // Pulses a rebalance at intervals while a projectile is airborne.
        public void Tick()
        {
            TickFlightRebalance(Time.deltaTime);
        }

        public void ResetRun(int generation)
        {
            // Bumping the generation drops any balance already scheduled this frame.
            _generation = generation;
            _balanceRequested = false;
            _activeProjectile = null;
            _flightRebalanceElapsed = 0f;
            _turnSteps.Clear();
            _motionTicker.CancelAllBalanceMoves();
            ReleasePaths();
        }

        private void AnimatePaths(Dictionary<IWriteableDynamicSlotActor, List<Vector3>> paths)
        {
            foreach (var (actor, path) in paths)
            {
                var slot = actor.SlotIndex.Value;
                var view = _grid.ViewAt(slot);
                if (view == null)
                {
                    throw new InvalidOperationException(
                        $"BalloonBalancer.AnimatePaths: no view found at slot ({slot.x},{slot.y}) " +
                        "— model/view desync.");
                }

                view.TweenTracker.Kill();
                view.transform.DOKill();

                StartBalanceMove(actor, path, view);
            }
        }

        private void StartBalanceMove(
            IWriteableDynamicSlotActor actor, List<Vector3> path, ISlotActorView view)
        {
            var viewTransform = view.transform;
            var currentScale = viewTransform.localScale;

            // Sever any in-flight spawn tween (DOPath/DOScale) so it doesn't fight the ticker's drive.
            view.TweenTracker.Kill();
            viewTransform.DOKill();

            // Direct movers skip the resolve's intermediate waypoints and glide straight to the end.
            var directMotion = actor is IBalanceInfluence { DirectBalanceMotion: true } && path.Count > 1;

            // A (near-)zero-length move has nothing to animate — complete it inline. (The old DOTween
            // path also NaN'd here, but a zero-length Catmull-Rom is simply a stationary point.)
            var travelSqr = directMotion
                ? (path[^1] - viewTransform.position).sqrMagnitude
                : PolylineSqrLength(viewTransform.position, path);
            if (travelSqr < DegenerateMoveSqrEpsilon)
            {
                actor.IsStable.Value = true;
                _balancePathHolder.Release(actor);
            }
            else
            {
                var waypoints = directMotion
                    ? FinalWaypointBuffer(viewTransform.position, path)
                    : WaypointBuffer(viewTransform.position, path);

                _motionTicker.StartBalanceMove(
                    (IBalloonMotionView)view,
                    waypoints,
                    _balloonsConfig.TimeForBalloonsBalance,
                    _disturbanceField,
                    _disturbanceField.GetProfile(StampSource.BalloonPath),
                    _finalizeBalanceMove,
                    actor);
            }

            if (currentScale != Vector3.one)
            {
                viewTransform.DOScale(Vector3.one, _balloonsConfig.TimeForBalloonsBalance);
            }
        }

        // Cached completion for a ticker balance move: settle the actor and free its reserved transit
        // slots. Cached as a field delegate so a move schedules no per-move closure.
        private void FinalizeBalanceMove(object payload)
        {
            var actor = (IWriteableDynamicSlotActor)payload;
            actor.IsStable.Value = true;
            _balancePathHolder.Release(actor);
        }

        // relocateRoamers: true only at the turn boundary (pre-spawn), so roaming types jump once per turn,
        // not on every deferred/flight rebalance. A direct run services any pending deferred request, so
        // the death-frame publish and the spawner's pre-spawn call coalesce into one sweep.
        internal void Balance(bool relocateRoamers = false)
        {
            _balanceRequested = false;
            ReleasePaths();

            if (relocateRoamers)
            {
                RelocateRoamers();
            }

            _balanceMoves.Clear();
            _balancePlanner.Plan(_turnSteps, _balanceMoves);
            ApplyBalanceMoves();

            AnimatePaths(_paths);
            ReleasePaths();
        }

        // BalancePathHolder.Reserve only tracks transit slots for its own bookkeeping — it never reads
        // grid state — so reserving each move's slots here, after the planner already mutated the grid
        // for every pass, is behaviorally identical to the pre-refactor code's per-move reserve-then-
        // mutate order. IsStable and RecordPath are likewise deferred: nothing during planning reads
        // either, and RecordPath's append order still matches execution order (an actor moving twice
        // appears twice, in order).
        private void ApplyBalanceMoves()
        {
            foreach (var move in _balanceMoves)
            {
                _balancePathHolder.Reserve(move.Actor, move.From);
                _balancePathHolder.Reserve(move.Actor, move.To);
                move.Actor.IsStable.Value = false;
                RecordPath(move.Actor, _grid.IndexToWorldPosition(move.To));
            }
        }

        // Pre-pass of the race: each IPreBalanceRelocatable defines its own placement; the balancer only
        // supplies the legal resting slots and invokes the contract in priority order. The balance rounds
        // then settle everything around the new positions.
        private void RelocateRoamers()
        {
            _roamers.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var actor = _grid.At(new Vector2Int(col, row));
                    if (actor is IPreBalanceRelocatable && actor is IWriteableDynamicSlotActor dynamicActor)
                    {
                        _roamers.Add(new RoamCandidate
                        {
                            Actor = dynamicActor,
                            Priority = (actor as IBalanceInfluence)?.BalancePriority ?? 0,
                            Order = _roamers.Count,
                        });
                    }
                }
            }

            _roamers.Sort(ByRoamOrder);

            foreach (var roamer in _roamers)
            {
                var actor = roamer.Actor;
                CollectRestingSlots();
                if (!((IPreBalanceRelocatable)actor).TryPickRelocation(_grid, _restingSlots, out var target))
                {
                    continue;
                }

                var from = actor.SlotIndex.Value;
                var view = _grid.ViewAt(from);

                _balancePathHolder.Reserve(actor, from);
                _balancePathHolder.Reserve(actor, target);

                _grid.Remove(from);
                _grid.Place(actor, view, target);
                actor.IsStable.Value = false;

                RecordPath(actor, _grid.IndexToWorldPosition(target));
            }
        }

        // Empty slots that need no further settling (row 0 or fully supported) — legal roam destinations.
        private void CollectRestingSlots()
        {
            _restingSlots.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row) && !_balanceQuery.IsUnbalanced(col, row))
                    {
                        _restingSlots.Add(new Vector2Int(col, row));
                    }
                }
            }
        }

        private void RecordPath(IWriteableDynamicSlotActor actor, Vector3 targetPosition)
        {
            if (_paths.TryGetValue(actor, out var path))
            {
                path.Add(targetPosition);
            }
            else
            {
                var newPath = ListPool<Vector3>.Get();
                newPath.Add(targetPosition);
                _paths[actor] = newPath;
            }
        }

        // Shoves a column's bottom occupant toward a gap so a new balloon can spawn. Returns whether room was opened.
        internal bool TryRelievePressure(int column)
        {
            if (!_pressurePropagation.TryResolve(column, _pressureMoves))
            {
                return false;
            }

            ReleasePaths();

            // Mover-first order: every destination is already vacant when its move executes.
            foreach (var move in _pressureMoves)
            {
                var view = _grid.ViewAt(move.From);

                _balancePathHolder.Reserve(move.Actor, move.From);
                _balancePathHolder.Reserve(move.Actor, move.To);

                _grid.Remove(move.From);
                _grid.Place(move.Actor, view, move.To);
                move.Actor.IsStable.Value = false;

                RecordPath(move.Actor, _grid.IndexToWorldPosition(move.To));
            }

            AnimatePaths(_paths);
            ReleasePaths();
            return true;
        }

        // Seeds the view's current position as waypoint 0 so a CatmullRom path always has ≥2 points:
        // DOTween's FinalizePath unconditionally reads wps[1], and its own prepend check finds
        // wps[0] ≈ current and adds nothing, so a degenerate zero-length path tweens harmlessly in place.
        // DOTween's Path constructor clones the waypoints (verified against the vendored dll), so one
        // shared buffer per path length is safe to reuse immediately.
        private Vector3[] WaypointBuffer(Vector3 currentPosition, IReadOnlyList<Vector3> path)
        {
            var count = path.Count + 1;
            if (!_waypointBuffers.TryGetValue(count, out var buffer))
            {
                buffer = new Vector3[count];
                _waypointBuffers[count] = buffer;
            }

            buffer[0] = currentPosition;
            for (var i = 0; i < path.Count; i++)
            {
                buffer[i + 1] = path[i];
            }

            return buffer;
        }

        // A two-slot reuse of the waypoint-buffer scheme: the current position then the path's final
        // position (see WaypointBuffer for why waypoint 0 is the current position).
        private Vector3[] FinalWaypointBuffer(Vector3 currentPosition, IReadOnlyList<Vector3> path)
        {
            if (!_waypointBuffers.TryGetValue(2, out var buffer))
            {
                buffer = new Vector3[2];
                _waypointBuffers[2] = buffer;
            }

            buffer[0] = currentPosition;
            buffer[1] = path[^1];
            return buffer;
        }

        // Sum of squared segment lengths from the current position through every waypoint. A path that
        // loops back near its start but travels meaningfully keeps a large sum, so it still animates;
        // only a genuinely motionless move reads as degenerate.
        private static float PolylineSqrLength(Vector3 start, IReadOnlyList<Vector3> path)
        {
            var total = 0f;
            var previous = start;
            for (var i = 0; i < path.Count; i++)
            {
                total += (path[i] - previous).sqrMagnitude;
                previous = path[i];
            }

            return total;
        }

        private void ReleasePaths()
        {
            foreach (var path in _paths.Values)
            {
                ListPool<Vector3>.Release(path);
            }

            _paths.Clear();
        }

        private async UniTaskVoid BalanceNextFrameAsync(int generation)
        {
            await UniTask.Yield();
            RunScheduledBalance(generation);
        }

        // Runs the deferred balance unless a reset bumped the generation or a direct run already
        // serviced the request.
        internal bool RunScheduledBalance(int generation)
        {
            if (generation != _generation || !_balanceRequested)
            {
                return false;
            }

            Balance();
            return true;
        }

        // Internal for tests — Time.deltaTime isn't injectable, so tests drive it with an explicit step.
        internal bool TickFlightRebalance(float deltaTime)
        {
            var interval = _balloonsConfig.FlightRebalanceInterval;
            if (interval <= 0f || _activeProjectile == null || !_activeProjectile.IsFree
                || _activeProjectile.IsLastShieldApproach.Value
                || _pauseService.IsAnyPaused.Value)
            {
                // Freeze the board while a doomed shot glides its last-breath segment: with respawn
                // already held (BalloonSpawner) and rebalance stopped here, nothing can drift into the
                // computed glide path, so the eased traversal is safe for any curve. Resumes when the
                // shot dies and a fresh one loads (IsLastShieldApproach clears with the new model).
                _flightRebalanceElapsed = 0f;
                return false;
            }

            _flightRebalanceElapsed += deltaTime;
            if (_flightRebalanceElapsed < interval)
            {
                return false;
            }

            _flightRebalanceElapsed = 0f;

            if (!HasPossibleMove())
            {
                return false;
            }

            // Each pulse opens a fresh step window: pulses are already paced by the interval, so a
            // 1-step heavy crawls one slot per pulse instead of sitting exhausted for the whole flight
            // (the shared budget exists to stop multi-hops within the turn's clustered runs).
            _turnSteps.Clear();
            RequestBalance();
            return true;
        }

        // True when some occupied dynamic actor could shift; read-only.
        internal bool HasPossibleMove()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 1; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row) || !_balanceQuery.IsUnbalanced(col, row))
                    {
                        continue;
                    }

                    if (_grid.At(new Vector2Int(col, row)) is IWriteableDynamicSlotActor
                        && _balanceQuery.OptimalNextEmptySlot(col, row).HasValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // The turn boundary: the heavy-step budget refreshes here, covering the death-frame balances,
        // the spawn wave's, and the next flight's pulses as one allowance.
        private void OnProjectileDestroyed()
        {
            _activeProjectile = null;
            _turnSteps.Clear();
        }

        private void RequestBalance()
        {
            if (_balanceRequested)
            {
                return;
            }

            _balanceRequested = true;
            BalanceNextFrameAsync(_generation).Forget();
        }

        private struct RoamCandidate
        {
            public IWriteableDynamicSlotActor Actor;
            public int Priority;
            public int Order;
        }
    }
}
