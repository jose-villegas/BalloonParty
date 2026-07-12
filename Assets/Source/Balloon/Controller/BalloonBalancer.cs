using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using UnityEngine.Pool;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Balloon.Controller
{
    internal class BalloonBalancer : IStartable, ITickable, IRunResettable
    {
        // Higher priority intervenes first; equal priorities keep the sweep's original order.
        private static readonly Comparison<PassCandidate> ByInterventionOrder = (a, b) =>
            a.Priority != b.Priority ? b.Priority - a.Priority : a.Order - b.Order;

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
        private readonly PressurePropagation _pressurePropagation;
        private readonly Dictionary<IWriteableDynamicSlotActor, List<Vector3>> _paths = new();
        private readonly List<PressurePropagation.Move> _pressureMoves = new();
        private readonly Dictionary<IWriteableDynamicSlotActor, int> _turnSteps = new();
        private readonly List<PassCandidate> _passCandidates = new();
        private readonly List<RoamCandidate> _roamers = new();
        private readonly List<Vector2Int> _restingSlots = new();
        private readonly Dictionary<int, Vector3[]> _waypointBuffers = new();

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
            _pressurePropagation = new PressurePropagation(grid, balanceQuery.Evaluator, debugRecorder);
        }

        public void Start()
        {
            _subscriber.Subscribe(_ => RequestBalance());
            _projectileLoadedSubscriber.Subscribe(msg => _activeProjectile = msg.Model);
            _projectileDestroyedSubscriber.Subscribe(_ => OnProjectileDestroyed());
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

                // A ticker-driven nudge escapes DOKill — cancel it explicitly.
                if (view is View.IBalloonMotionView motionView)
                {
                    _motionTicker.CancelNudge(motionView);
                }

                var currentScale = view.transform.localScale;
                var viewTransform = view.transform;

                // Direct movers skip the resolve's intermediate waypoints and tween straight to the end.
                var waypoints = actor is IBalanceInfluence { DirectBalanceMotion: true } && path.Count > 1
                    ? FinalWaypointBuffer(path)
                    : WaypointBuffer(path);

                var tween = viewTransform
                    .DOPath(waypoints, _balloonsConfig.TimeForBalloonsBalance, PathType.CatmullRom)
                    .StampDisturbanceAlongPath(viewTransform, _disturbanceField, StampSource.BalloonPath)
                    .OnComplete(() =>
                    {
                        actor.IsStable.Value = true;
                        _balancePathHolder.Release(actor);
                    });

                view.TweenTracker.Append(tween);

                if (currentScale != Vector3.one)
                {
                    view.transform.DOScale(Vector3.one, _balloonsConfig.TimeForBalloonsBalance);
                }
            }
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

            while (BalanceOnePass())
            {
            }

            AnimatePaths(_paths);
            ReleasePaths();
        }

        // One round of the race: every unbalanced actor gets a move attempt in intervention order —
        // faster types act first and win contested slots. Returns whether any actor was shifted.
        private bool BalanceOnePass()
        {
            CollectPassCandidates();

            var moved = false;
            for (var i = 0; i < _passCandidates.Count; i++)
            {
                var slot = _passCandidates[i].Slot;
                moved |= TryBalanceSlot(slot.x, slot.y);
            }

            return moved;
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

        // Snapshots this round's unbalanced actors, ordered by their BalancePriority. Earlier moves can
        // invalidate a candidate; TryBalanceSlot re-validates everything, so a stale entry is a no-op.
        private void CollectPassCandidates()
        {
            _passCandidates.Clear();

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 1; row < _grid.Rows; row++)
                {
                    if (_grid.IsEmpty(col, row) || !_balanceQuery.IsUnbalanced(col, row))
                    {
                        continue;
                    }

                    var slot = new Vector2Int(col, row);
                    _passCandidates.Add(new PassCandidate
                    {
                        Slot = slot,
                        Priority = (_grid.At(slot) as IBalanceInfluence)?.BalancePriority ?? 0,
                        Order = _passCandidates.Count,
                    });
                }
            }

            _passCandidates.Sort(ByInterventionOrder);
        }

        // Shifts the actor at (col, row) toward its optimal empty slot. Returns whether it moved.
        private bool TryBalanceSlot(int col, int row)
        {
            if (_grid.IsEmpty(col, row))
            {
                return false;
            }

            if (!_balanceQuery.IsUnbalanced(col, row))
            {
                return false;
            }

            var currentSlot = new Vector2Int(col, row);
            if (_grid.At(currentSlot) is not IWriteableDynamicSlotActor dynamicActor)
            {
                return false;
            }

            // Physical weight: a heavy actor only moves so many slots per TURN. The budget spans every
            // balance run between projectile deaths (pre/post-spawn, flight pulses) — a per-run cap would
            // grant one step per run and read as multi-step hops.
            var stepCap = (dynamicActor as IBalanceInfluence)?.MaxBalanceSteps ?? 0;
            if (stepCap > 0 && _turnSteps.GetValueOrDefault(dynamicActor) >= stepCap)
            {
                return false;
            }

            var nextSlot = _balanceQuery.OptimalNextEmptySlot(col, row);
            if (!nextSlot.HasValue)
            {
                return false;
            }

            _balancePathHolder.Reserve(dynamicActor, currentSlot);
            _balancePathHolder.Reserve(dynamicActor, nextSlot.Value);

            var actorView = _grid.ViewAt(currentSlot);
            _grid.Remove(currentSlot);
            _grid.Place(dynamicActor, actorView, nextSlot.Value);
            dynamicActor.IsStable.Value = false;

            RecordPath(dynamicActor, _grid.IndexToWorldPosition(nextSlot.Value));
            if (stepCap > 0)
            {
                _turnSteps[dynamicActor] = _turnSteps.GetValueOrDefault(dynamicActor) + 1;
            }

            return true;
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

        // DOTween's Path constructor clones the waypoints (verified against the vendored dll), so one
        // shared buffer per path length is safe to reuse immediately.
        private Vector3[] WaypointBuffer(IReadOnlyList<Vector3> path)
        {
            if (!_waypointBuffers.TryGetValue(path.Count, out var buffer))
            {
                buffer = new Vector3[path.Count];
                _waypointBuffers[path.Count] = buffer;
            }

            for (var i = 0; i < path.Count; i++)
            {
                buffer[i] = path[i];
            }

            return buffer;
        }

        // A one-slot reuse of the waypoint-buffer scheme, holding only the path's final position.
        private Vector3[] FinalWaypointBuffer(IReadOnlyList<Vector3> path)
        {
            if (!_waypointBuffers.TryGetValue(1, out var buffer))
            {
                buffer = new Vector3[1];
                _waypointBuffers[1] = buffer;
            }

            buffer[0] = path[^1];
            return buffer;
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
                || _pauseService.IsAnyPaused.Value)
            {
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

        private struct PassCandidate
        {
            public Vector2Int Slot;
            public int Priority;
            public int Order;
        }

        private struct RoamCandidate
        {
            public IWriteableDynamicSlotActor Actor;
            public int Priority;
            public int Order;
        }
    }
}
