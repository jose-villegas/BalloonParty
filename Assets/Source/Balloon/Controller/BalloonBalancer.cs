using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Projectile;
using BalloonParty.Balloon.View;
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
        private readonly IProjectileRegistry _projectiles;
        private readonly PauseService _pauseService;
        private readonly DisturbanceFieldService _disturbanceField;
        private readonly BalloonMotionTicker _motionTicker;
        private readonly Action<object> _finalizeBalanceMove;
        private readonly PressurePropagation _pressurePropagation;
        private readonly BalancePlanner _balancePlanner;
        private readonly UnbreakableFormationService _formations;
        private readonly Action<IWriteableDynamicSlotActor, Vector2Int> _relocateActor;
        private readonly Action<IReadOnlyList<SlotMove>> _relocateActors;
        private readonly List<ISlotActorView> _liftedViews = new();
        private readonly Dictionary<IWriteableDynamicSlotActor, List<Vector3>> _paths = new();
        private readonly List<PressurePropagation.Move> _pressureMoves = new();
        private readonly List<BalanceMove> _balanceMoves = new();
        private readonly Dictionary<IWriteableDynamicSlotActor, int> _turnSteps = new();
        private readonly List<RoamCandidate> _roamers = new();
        private readonly RoamRests _roamRests = new();
        private readonly List<Vector2Int> _restingSlots = new();
        private readonly Dictionary<int, Vector3[]> _waypointBuffers = new();
        private readonly CompositeDisposable _subscriptions = new();

        private bool _balanceRequested;
        private bool _snakeStepDue;
        private bool _cycleDue;
        private bool _formDue;
        private bool _toughGatherDue;
        private bool _toughStepDue;
        private int _generation;
        private float _flightRebalanceElapsed;
        private float _beatElapsed;
        private float _toughStepElapsed;
        private int _beats;
        private int _snakeSteps;

        public int ResetOrder => RunResetOrder.Quiesce;

        // Exposed for tests.
        internal int Generation => _generation;

        // For the shot solver: the pulse runs all through play, so a shot fired now meets its first one
        // this soon, not a full interval after the fire.
        internal float SecondsUntilNextPulse =>
            Mathf.Max(0f, _balloonsConfig.FlightRebalanceInterval - _flightRebalanceElapsed);

        [Inject]
        internal BalloonBalancer(
            SlotGrid grid,
            GridBalanceQuery balanceQuery,
            IBalloonsConfiguration balloonsConfig,
            BalancePathHolder balancePathHolder,
            ISubscriber<BalanceBalloonsMessage> subscriber,
            IProjectileRegistry projectiles,
            PauseService pauseService,
            DisturbanceFieldService disturbanceField,
            BalloonMotionTicker motionTicker,
            BalanceDebugRecorder debugRecorder = null,
            UnbreakableFormationService formations = null)
        {
            _grid = grid;
            _balanceQuery = balanceQuery;
            _balloonsConfig = balloonsConfig;
            _balancePathHolder = balancePathHolder;
            _subscriber = subscriber;
            _projectiles = projectiles;
            _pauseService = pauseService;
            _disturbanceField = disturbanceField;
            _motionTicker = motionTicker;
            _finalizeBalanceMove = FinalizeBalanceMove;
            _pressurePropagation = new PressurePropagation(grid, balanceQuery.Evaluator, debugRecorder);
            _balancePlanner = new BalancePlanner(grid, balanceQuery);
            _formations = formations;
            _relocateActor = RelocateActor;
            _relocateActors = RelocateActors;
        }

        public void Start()
        {
            _subscriber.Subscribe(_ => RequestBalance()).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // Pulses a rebalance at intervals all through play: lines arrive on a clock, not only after a shot.
        public void Tick()
        {
            TickFlightRebalance(Time.deltaTime);
        }

        public void ResetRun(int generation)
        {
            // Bumping the generation drops any balance already scheduled this frame.
            _generation = generation;
            _balanceRequested = false;
            _snakeStepDue = false;
            _cycleDue = false;
            _formDue = false;
            _toughGatherDue = false;
            _toughStepDue = false;
            _flightRebalanceElapsed = 0f;
            _beatElapsed = 0f;
            _toughStepElapsed = 0f;
            _beats = 0;
            _snakeSteps = 0;
            _roamRests.Clear();
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

                // Distance ÷ speed keeps the settle at a constant speed: a three-cell rise takes three
                // times as long as a one-cell hop instead of both racing to a fixed duration.
                var speed = _balloonsConfig.ResolveMoveSpeed(
                    actor is IBalanceInfluence influence ? influence.MoveSpeed : 0f);

                _motionTicker.StartBalanceMove(
                    (IBalloonMotionView)view,
                    waypoints,
                    waypoints.PolylineLength() / speed,
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

        // atWaveStart: true only for the spawner's sweep before a wave's first line. The formations roll for
        // reinforcements there, and the heavy-step budget refreshes there, covering the wave's sweeps and the pulses
        // until the next wave as one allowance. A direct run services any pending deferred request, so a publish in
        // the same frame coalesces into one sweep.
        internal void Balance(bool atWaveStart = false)
        {
            _balanceRequested = false;
            ReleasePaths();

            if (atWaveStart)
            {
                _turnSteps.Clear();
                _formations?.RollReinforcementsAtWaveStart();
            }

            // The unbreakables keep one clock: formations change shape, and a lone one moves, only on a cycle; free
            // ones form up on any beat.
            var arranged = _cycleDue || _formDue;
            if (_cycleDue)
            {
                // Formations go first, so the lone unbreakable sees the formation's slots as taken.
                _formations?.ArrangeOnCycle(_relocateActors);
                RelocateOneRoamer();
            }
            else if (_formDue)
            {
                _formations?.FormOnBeat(_relocateActors);
            }

            _cycleDue = false;
            _formDue = false;

            // The toughs form up the moment they can, before the race, so the board settles around them: a tough
            // that a line just brought flies from its entry straight to the snake's tail.
            if (_toughGatherDue || (_formations?.ToughsDueToGather() ?? false))
            {
                _toughGatherDue = false;
                _formations?.GatherToughSnake(_relocateActors);
            }

            // A snake's step rides the beat that called it, before the race, so the board settles around it.
            if (_snakeStepDue)
            {
                _snakeStepDue = false;
                _formations?.StepSnakes(_relocateActors, afterArranging: arranged);
            }

            if (_toughStepDue)
            {
                _toughStepDue = false;
                _formations?.StepToughSnake(_relocateActors, afterArranging: arranged);
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

        // Pre-pass of the race, once a cycle: each IPreBalanceRelocatable defines its own placement; the balancer
        // only supplies the legal resting slots and invokes the contract. Only one moves alone per cycle, the
        // longest at rest of those that have stood LoneHoldCycles, and none while the last one is still on its way,
        // so the lone ones keep the formations' pace instead of all crossing the board at every wave. The balance
        // rounds then settle everything around the new position.
        private void RelocateOneRoamer()
        {
            _roamRests.BeginCycle();
            CollectRoamers();
            _roamRests.EndCycle();
            if (_formations == null || _roamRests.LastMoverOnItsWay)
            {
                return;
            }

            _roamers.Sort(ByRoamOrder);
            foreach (var roamer in _roamers)
            {
                var actor = roamer.Actor;

                // A lance's plowed one stays where it was struck until the discharge shatters it.
                if (_roamRests.CyclesAtRest(actor) < _formations.LoneHoldCycles
                    || (actor is IBalloonModel balloon && _projectiles.AwaitsDischarge(balloon)))
                {
                    continue;
                }

                CollectRestingSlots();
                if (ShotWall.Walls(actor))
                {
                    _formations.DropWallClosingSlots(actor.SlotIndex.Value, _restingSlots);
                }

                if (!((IPreBalanceRelocatable)actor).TryPickRelocation(_grid, _restingSlots, out var target))
                {
                    continue;
                }

                RelocateActor(actor, target);
                _roamRests.Moved(actor);
                return;
            }
        }

        // The free roamers on the board, each a cycle further into its rest (a new one starts its rest now), by
        // priority, then the longest at rest first.
        private void CollectRoamers()
        {
            _roamers.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var actor = _grid.At(new Vector2Int(col, row));
                    if (actor is not IPreBalanceRelocatable || actor is not IWriteableDynamicSlotActor dynamicActor
                        || actor is IBalanceAnchor { IsAnchored: true })
                    {
                        continue;
                    }

                    var rest = _roamRests.Stand(dynamicActor);
                    _roamers.Add(new RoamCandidate
                    {
                        Actor = dynamicActor,
                        Priority = (actor as IBalanceInfluence)?.BalancePriority ?? 0,
                        Order = -rest,
                    });
                }
            }
        }

        // One cycle's jump (a roam or a formation move): the grid changes now, the glide is animated
        // with the rest of this balance's paths.
        private void RelocateActor(IWriteableDynamicSlotActor actor, Vector2Int target)
        {
            var from = actor.SlotIndex.Value;
            var view = _grid.ViewAt(from);

            _balancePathHolder.Reserve(actor, from);
            _balancePathHolder.Reserve(actor, target);

            _grid.Remove(from);
            _grid.Place(actor, view, target);
            actor.IsStable.Value = false;

            RecordPath(actor, _grid.IndexToWorldPosition(target));
        }

        // A batch of formation moves at once: every actor leaves its slot before any lands, so the batch may trade
        // slots among its actors (a tough snake walking into a balloon sends it round to the slot its tail leaves).
        private void RelocateActors(IReadOnlyList<SlotMove> moves)
        {
            _liftedViews.Clear();
            for (var i = 0; i < moves.Count; i++)
            {
                var actor = moves[i].Actor;
                var from = actor.SlotIndex.Value;
                _liftedViews.Add(_grid.ViewAt(from));
                _balancePathHolder.Reserve(actor, from);
                _balancePathHolder.Reserve(actor, moves[i].Target);
                _grid.Remove(from);
            }

            for (var i = 0; i < moves.Count; i++)
            {
                var actor = moves[i].Actor;
                _grid.Place(actor, _liftedViews[i], moves[i].Target);
                actor.IsStable.Value = false;
                RecordPath(actor, _grid.IndexToWorldPosition(moves[i].Target));
            }

            _liftedViews.Clear();
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
            var paused = _pauseService.IsAnyPaused.Value;
            var doomed = _projectiles.DoomedShotFliesAlone();
            if (interval <= 0f || doomed || paused)
            {
                // Freeze the board while a lone doomed shot glides its last-breath segment: with the spawn clock
                // already held (SpawnClock) and rebalance stopped here, nothing drifts into the glide path but an
                // unbreakable snake's step, which at most turns the shot back from doomed. Resumes when the shot
                // dies. One ball's doom never freezes the board under other balls. The tough snake's clock only
                // holds: nearly every shot ends in a doomed glide, so restarting it would stop the snake for good.
                _flightRebalanceElapsed = 0f;
                FormToughsInTheLevelTransition();
                if (doomed && !paused && interval > 0f)
                {
                    KeepTheBeatThroughADoomedGlide(deltaTime);
                }

                return false;
            }

            _flightRebalanceElapsed += deltaTime;
            var pulse = _flightRebalanceElapsed >= interval;
            var formationDue = TickFormations(deltaTime);
            if (!pulse && !formationDue)
            {
                return false;
            }

            if (pulse)
            {
                _flightRebalanceElapsed = 0f;
            }

            // A snake steps on its clock, and the toughs gather, even when nothing else would move.
            if (!formationDue && !HasPossibleMove())
            {
                return false;
            }

            // Each pulse opens a fresh step window: pulses are already paced by the interval, so a
            // 1-step heavy crawls one slot per pulse instead of sitting exhausted until the next wave
            // (the shared budget exists to stop multi-hops within one sweep's clustered runs). The snakes'
            // quicker steps and the toughs forming up open none, so the heavies keep the board's pace.
            if (pulse)
            {
                _turnSteps.Clear();
            }

            RequestBalance();
            return true;
        }

        // A level's toughs rise during its transition, which holds the board. They form up as they rise all the
        // same, so the snake is one body before play starts; nothing else moves, no step and no balance. Any
        // other hold (a pause, a doomed glide) stops them too.
        private void FormToughsInTheLevelTransition()
        {
            if (_formations == null || !_pauseService.IsPausedOnlyBy(PauseSource.LevelTransition)
                || _projectiles.DoomedShotFliesAlone() || !_formations.ToughsDueToGather())
            {
                return;
            }

            ReleasePaths();
            _formations.GatherToughSnake(_relocateActors);
            AnimatePaths(_paths);
            ReleasePaths();
        }

        // The unbreakables' rhythm comes first, so their beat runs on through a doomed glide, and an unbreakable
        // snake steps on its tick right there. A cycle or a form-up that falls due waits for the balance after the
        // glide, as everything else on the board does.
        private void KeepTheBeatThroughADoomedGlide(float deltaTime)
        {
            if (_formations == null || !TickBeat(deltaTime) || !_snakeStepDue)
            {
                return;
            }

            _snakeStepDue = false;
            ReleasePaths();
            _formations.StepSnakes(_relocateActors);
            AnimatePaths(_paths);
            ReleasePaths();
        }

        // Ticks the unbreakables' beat and the tough snake's step clock and asks whether the toughs should form up,
        // marking each that falls due for the next balance. True when any did.
        private bool TickFormations(float deltaTime)
        {
            if (_formations == null)
            {
                return false;
            }

            var beat = TickBeat(deltaTime);

            // The toughs don't wait for a step to form up: a tough that lands falls in at once.
            var gather = _formations.ToughsDueToGather();
            var toughStep = TickStepClock(ref _toughStepElapsed, _formations.ToughSnakeStepSeconds,
                _formations.HasToughSnake, _formations.ToughSnakeLanded, deltaTime);
            _toughGatherDue |= gather;
            _toughStepDue |= toughStep;
            return beat || gather || toughStep;
        }

        // The unbreakables' clock, marking what falls due. True when anything did. An unbreakable snake steps
        // SnakeStepsPerBeat times a beat, on the beat and evenly between; on a beat the free unbreakables form up;
        // every BeatsPerCycle beats is a cycle, when formations also change shape and a lone unbreakable may move. The
        // clock runs with no snake too, so the snakes' steps and the cycles keep one rhythm.
        private bool TickBeat(float deltaTime)
        {
            var stepsPerBeat = _formations.SnakeStepsPerBeat;
            if (!TickStepClock(ref _beatElapsed, _formations.BeatSeconds / stepsPerBeat, true, true, deltaTime))
            {
                return false;
            }

            _snakeSteps++;
            var snakeStep = _formations.HasUnbreakableSnake;
            var cycle = false;
            var form = false;
            if (_snakeSteps % stepsPerBeat == 0)
            {
                _beats++;
                cycle = _beats % _formations.BeatsPerCycle == 0 && _formations.HasUnbreakables;
                form = !cycle && _formations.UnbreakablesDueToForm();
            }

            _snakeStepDue |= snakeStep;
            _cycleDue |= cycle;
            _formDue |= form;
            return snakeStep || cycle || form;
        }

        // A snake's step clock: true every interval of play while one walks. A pause or a doomed glide holds it
        // (the caller doesn't tick it then) where the pulse starts over, since nearly every shot ends in a doomed
        // glide. With no snake it stands ready, so a new snake takes its first step the moment it forms, or, while
        // a member is still rising into place (ready false), the moment it lands: the toughs that rise together at
        // a level's start are moving as soon as they land. A step that falls due unready waits for it.
        internal static bool TickStepClock(ref float elapsed, float interval, bool walking, bool ready, float deltaTime)
        {
            if (interval <= 0f)
            {
                elapsed = 0f;
                return false;
            }

            if (!walking)
            {
                elapsed = interval;
                return false;
            }

            elapsed += deltaTime;
            if (elapsed < interval || !ready)
            {
                return false;
            }

            elapsed = 0f;
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

                    var actor = _grid.At(new Vector2Int(col, row));
                    if (actor is IWriteableDynamicSlotActor and not IBalanceAnchor { IsAnchored: true }
                        && _balanceQuery.OptimalNextEmptySlot(col, row).HasValue)
                    {
                        return true;
                    }
                }
            }

            return false;
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
