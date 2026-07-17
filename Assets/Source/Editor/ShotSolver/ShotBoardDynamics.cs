using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Nudge;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Editor.ShotSolver
{
    /// <summary>Owns a real headless <c>SlotGrid</c> + <c>GridBalanceQuery</c> + <c>BalancePlanner</c>
    /// over stub actors, plus the per-balloon nudge-impulse bookkeeping — the dynamic-board half of
    /// @ref plan_shot_geometry §7 (tasks 4b/4c). Reused across an entire sweep's worth of angles: build
    /// once from a gathered board, then call <see cref="ResetForNewFlight" /> at the start of every
    /// <see cref="ShotSimulator.Simulate" /> call rather than reallocating (mirrors the sim's
    /// caller-owned <c>workingSet</c> pattern).</summary>
    internal sealed class ShotBoardDynamics
    {
        // Safety valve against a pathological config (e.g. a near-zero interval) looping the pulse
        // scheduler forever — of the same order as ShotSimulator.DefaultMaxEvents.
        private const int MaxPulsesPerFlight = 4000;

        private readonly SlotGrid _grid;
        private readonly GridBalanceQuery _balanceQuery;
        private readonly BalancePlanner _planner;
        private readonly NudgeOverrideResolver _nudgeResolver;
        private readonly float _flightRebalanceInterval;
        private readonly float _pulseExecutionDelay;
        private readonly float _balanceDuration;
        private readonly IReadOnlyList<ShotBalloonSnapshot> _targets;
        private readonly IReadOnlyList<ShotDynamicActorSnapshot> _otherDynamicSnapshots;
        private readonly IReadOnlyList<ShotStaticActorSnapshot> _staticSnapshots;
        private readonly ShotSimDynamicActor[] _targetActors;
        private readonly ShotSimDynamicActor[] _otherDynamicActors;
        private readonly ShotSimStaticActor[] _staticActors;
        private readonly Dictionary<IWriteableDynamicSlotActor, int> _turnSteps = new();
        private readonly List<BalanceMove> _moves = new();
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];

        private float _nextPulseTime;
        private int _pulsesRun;

        /// <summary>Same order as the <c>board</c> list passed to <see cref="ShotSimulator.Simulate" /> —
        /// <c>TargetActors[i]</c> is the actor backing <c>board[i]</c>.</summary>
        internal IReadOnlyList<ShotSimDynamicActor> TargetActors => _targetActors;

        internal ShotBoardDynamics(
            IGameConfiguration gameConfig,
            IBalloonsConfiguration balloonsConfig,
            IReadOnlyList<ShotBalloonSnapshot> targets,
            IReadOnlyList<ShotDynamicActorSnapshot> otherDynamicActors,
            IReadOnlyList<ShotStaticActorSnapshot> staticActors,
            float pulseExecutionDelay = 0f)
        {
            _grid = new SlotGrid(gameConfig, new BalancePathHolder());
            _balanceQuery = new GridBalanceQuery(_grid);
            _planner = new BalancePlanner(_grid, _balanceQuery);
            _nudgeResolver = new NudgeOverrideResolver(balloonsConfig);
            _flightRebalanceInterval = balloonsConfig.FlightRebalanceInterval;

            // The live pulse lands late: TickFlightRebalance only notices the interval crossing at the
            // next render frame (0..1 frame of quantization) and RequestBalance defers the actual
            // Balance() one more frame (BalanceNextFrameAsync) — the caller estimates that lag from
            // its real frame time so the sim's moves start when the game's tweens actually do.
            _pulseExecutionDelay = Mathf.Max(pulseExecutionDelay, 0f);
            _balanceDuration = Mathf.Max(balloonsConfig.TimeForBalloonsBalance, 0.0001f);

            _targets = targets;
            _otherDynamicSnapshots = otherDynamicActors;
            _staticSnapshots = staticActors;

            _targetActors = new ShotSimDynamicActor[targets.Count];
            for (var i = 0; i < targets.Count; i++)
            {
                var snapshot = targets[i];
                _targetActors[i] = new ShotSimDynamicActor
                {
                    IsShotTarget = true,
                    BalancePriority = snapshot.BalancePriority,
                    MaxBalanceSteps = snapshot.MaxBalanceSteps,
                    DirectBalanceMotion = snapshot.DirectBalanceMotion,
                    NudgeOverrides = snapshot.NudgeOverrides,
                };
            }

            _otherDynamicActors = new ShotSimDynamicActor[otherDynamicActors.Count];
            for (var i = 0; i < otherDynamicActors.Count; i++)
            {
                var snapshot = otherDynamicActors[i];
                _otherDynamicActors[i] = new ShotSimDynamicActor
                {
                    IsShotTarget = false,
                    BalancePriority = snapshot.BalancePriority,
                    MaxBalanceSteps = snapshot.MaxBalanceSteps,
                    DirectBalanceMotion = snapshot.DirectBalanceMotion,
                };
            }

            _staticActors = new ShotSimStaticActor[staticActors.Count];
            for (var i = 0; i < staticActors.Count; i++)
            {
                _staticActors[i] = new ShotSimStaticActor();
            }
        }

        /// <summary>Re-derives the grid and every stub actor's mutable state from the gathered snapshot
        /// — called once at the top of every <see cref="ShotSimulator.Simulate" /> call so a sweep of
        /// thousands of angles never reallocates the board.</summary>
        internal void ResetForNewFlight()
        {
            ClearGrid();

            for (var i = 0; i < _targets.Count; i++)
            {
                var snapshot = _targets[i];
                _targetActors[i].ResetTo(snapshot.SlotIndex, snapshot.Position);
                _grid.Place(_targetActors[i], null, snapshot.SlotIndex);
            }

            for (var i = 0; i < _otherDynamicSnapshots.Count; i++)
            {
                var snapshot = _otherDynamicSnapshots[i];
                _otherDynamicActors[i].ResetTo(snapshot.SlotIndex, _grid.IndexToWorldPosition(snapshot.SlotIndex));
                _grid.Place(_otherDynamicActors[i], null, snapshot.SlotIndex);
            }

            for (var i = 0; i < _staticSnapshots.Count; i++)
            {
                _grid.Place(_staticActors[i], null, _staticSnapshots[i].SlotIndex);
            }

            _turnSteps.Clear();
            _nextPulseTime = _flightRebalanceInterval > 0f
                ? _flightRebalanceInterval + _pulseExecutionDelay
                : float.PositiveInfinity;
            _pulsesRun = 0;
        }

        /// <summary>If a balance pulse is due strictly before <paramref name="upperBoundTimeAbsolute" />
        /// (the next candidate projectile event), runs it and reports when. The caller must re-derive
        /// projectile events after this returns true — the board just changed.</summary>
        internal bool TryRunPulseIfDue(float upperBoundTimeAbsolute, out float pulseTimeAbsolute)
        {
            pulseTimeAbsolute = 0f;
            if (_nextPulseTime >= upperBoundTimeAbsolute || _pulsesRun >= MaxPulsesPerFlight)
            {
                return false;
            }

            pulseTimeAbsolute = _nextPulseTime;
            RunPulse(pulseTimeAbsolute);
            _nextPulseTime += _flightRebalanceInterval;
            _pulsesRun++;
            return true;
        }

        /// <summary>Every contact (pop or deflect) nudges the hit balloon's occupied hex neighbours —
        /// mirrors <c>NudgeService.OnActorHit</c> (<see cref="NudgeType.Neighbor" />); direction is the
        /// neighbour's slot LATTICE position minus the hit balloon's own slot lattice position, not the
        /// live wobble position — <c>NudgeService</c> itself works off <c>IndexToWorldPosition</c>, never
        /// the view.</summary>
        internal void OnBalloonHit(ShotSimDynamicActor hitActor, float tHit)
        {
            var hitSlot = hitActor.SlotIndex.Value;
            var hitSlotPos = _grid.IndexToWorldPosition(hitSlot);

            HexCoordinates.HexNeighborIndices(hitSlot.x, hitSlot.y, _neighborBuffer);
            for (var n = 0; n < 6; n++)
            {
                var neighborSlot = _neighborBuffer[n];
                if (_grid.IsEmpty(neighborSlot.x, neighborSlot.y))
                {
                    continue;
                }

                // Only shot-target balloons carry nudge state — a wobbling Unbreakable (not a
                // collidable target in this sim) would be inert to every observable output.
                if (_grid.At(neighborSlot) is not ShotSimDynamicActor { IsShotTarget: true } neighborActor)
                {
                    continue;
                }

                _nudgeResolver.Resolve(
                    neighborActor.NudgeOverrides, null, NudgeType.Neighbor, out var distance, out var duration);
                var direction = (Vector2)_grid.IndexToWorldPosition(neighborSlot) - (Vector2)hitSlotPos;
                AddImpulse(neighborActor, direction, distance, duration, tHit);
            }
        }

        /// <summary>The deflected balloon shoves itself along the projectile's INCOMING heading —
        /// mirrors <c>BalloonController.Deflect</c>'s <c>NudgeMessage</c> origin math
        /// (<c>slotPos − incomingDirection</c>), which <c>NudgeService.HandleSingleActor</c> turns back
        /// into direction == incoming direction.</summary>
        internal void OnBalloonDeflected(ShotSimDynamicActor actor, Vector2 incomingDirection, float tHit)
        {
            _nudgeResolver.Resolve(actor.NudgeOverrides, null, NudgeType.Deflect, out var distance, out var duration);
            AddImpulse(actor, incomingDirection, distance, duration, tHit);
        }

        /// <summary>Pops must open the gap for later pulses to see — the entire point of modelling the
        /// balance rules for real rather than freezing the board.</summary>
        internal void RemoveFromGrid(ShotSimDynamicActor actor)
        {
            _grid.Remove(actor.SlotIndex.Value);
        }

        private static void AddImpulse(
            ShotSimDynamicActor actor, Vector2 direction, float distance, float duration, float startTime)
        {
            if (direction.sqrMagnitude < 1e-8f)
            {
                return; // mirrors BalloonView.Nudge's own guard
            }

            var impulse = new ShotNudgeImpulse
            {
                Offset = direction.normalized * distance,
                StartTime = startTime,
                Duration = Mathf.Max(duration, 0.0001f), // mirrors BalloonMotionTicker.AddImpulse's floor
            };

            var impulses = actor.NudgeImpulses;
            PruneExpired(impulses, startTime);

            if (impulses.Count < ShotSimDynamicActor.MaxImpulses)
            {
                impulses.Add(impulse);
                return;
            }

            OverwriteMostComplete(impulses, impulse, startTime);
        }

        // Lazily drops impulses that would already have been pruned by per-frame Advance() in the live
        // ticker by the time this one arrives, so the 8-cap counts only what is genuinely still active.
        private static void PruneExpired(List<ShotNudgeImpulse> impulses, float t)
        {
            for (var i = impulses.Count - 1; i >= 0; i--)
            {
                var progress = (t - impulses[i].StartTime) / impulses[i].Duration;
                if (progress >= 1f)
                {
                    impulses[i] = impulses[^1];
                    impulses.RemoveAt(impulses.Count - 1);
                }
            }
        }

        // Cap reached — overwrite whichever impulse is closest to finishing, mirroring
        // BalloonMotionTicker.OverwriteMostComplete.
        private static void OverwriteMostComplete(List<ShotNudgeImpulse> impulses, ShotNudgeImpulse replacement, float t)
        {
            var replaceIndex = 0;
            var bestProgress = -1f;

            for (var i = 0; i < impulses.Count; i++)
            {
                var progress = (t - impulses[i].StartTime) / impulses[i].Duration;
                if (progress > bestProgress)
                {
                    bestProgress = progress;
                    replaceIndex = i;
                }
            }

            impulses[replaceIndex] = replacement;
        }

        private void ClearGrid()
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    _grid.Remove(new Vector2Int(col, row));
                }
            }
        }

        private void RunPulse(float pulseTime)
        {
            if (!HasPossibleMove())
            {
                return;
            }

            _turnSteps.Clear();
            _moves.Clear();
            _planner.Plan(_turnSteps, _moves);
            ApplyMoves(pulseTime);
        }

        // Same-pulse moves for one actor CHAIN as waypoints (mirroring RecordPath building the live
        // multi-waypoint DOPath); BeginBalanceMove itself distinguishes a fresh pulse from a chained
        // hop by the shared pulse start time, and collapses direct movers to their final target.
        private void ApplyMoves(float pulseTime)
        {
            foreach (var move in _moves)
            {
                if (move.Actor is ShotSimDynamicActor simActor)
                {
                    simActor.BeginBalanceMove(pulseTime, _grid.IndexToWorldPosition(move.To), _balanceDuration);
                }
            }
        }

        // Mirrors BalloonBalancer.HasPossibleMove.
        private bool HasPossibleMove()
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
    }
}
