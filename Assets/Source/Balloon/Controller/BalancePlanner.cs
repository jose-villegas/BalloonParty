using System;
using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     The balance race's pure decision core: sweeps unbalanced slots, moves each round's
    ///     highest-priority dynamic actor into its optimal empty slot, and repeats until a pass produces
    ///     no move. Grid mutation only (Remove/Place) — no views, tweens, or <c>IsStable</c> flips; the
    ///     caller re-derives those from the returned moves. <see cref="BalancePathHolder.Reserve" /> never
    ///     reads grid state, so a caller reserving each move's slots after <see cref="Plan" /> returns is
    ///     behaviorally identical to reserving immediately before each move (as the pre-refactor code did)
    ///     — nothing else synchronously consults transit-slot state mid-sweep.
    /// </summary>
    internal class BalancePlanner
    {
        // Higher priority intervenes first; equal priorities keep the sweep's original order.
        private static readonly Comparison<PassCandidate> ByInterventionOrder = (a, b) =>
            a.Priority != b.Priority ? b.Priority - a.Priority : a.Order - b.Order;

        private readonly SlotGrid _grid;
        private readonly GridBalanceQuery _balanceQuery;
        private readonly List<PassCandidate> _passCandidates = new();
        private readonly Dictionary<IWriteableDynamicSlotActor, HashSet<Vector2Int>> _visitedThisPlan = new();
        private readonly Stack<HashSet<Vector2Int>> _visitedPool = new();

        internal BalancePlanner(SlotGrid grid, GridBalanceQuery balanceQuery)
        {
            _grid = grid;
            _balanceQuery = balanceQuery;
        }

        // Runs every round of the race — each unbalanced actor gets a move attempt in intervention order,
        // faster types acting first and winning contested slots — until a round moves nothing. Appends
        // every move in execution order (an actor moving twice appears twice, letting the caller build a
        // multi-waypoint path) and mutates the grid immediately per move, so later candidates in the same
        // and later passes see the updated occupancy.
        internal void Plan(Dictionary<IWriteableDynamicSlotActor, int> turnSteps, List<BalanceMove> movesOut)
        {
            ReleaseVisited();

            // Convergence backstop: an UNCAPPED omnidirectional actor whose side/down moves score the
            // same 0 as its empty-cone up moves can ping-pong between two slots forever (the fallback
            // board's unbreakable crowds hit this — an unbounded loop that OOM-crashed via the growing
            // move list). The revisit guard below breaks every such cycle; this cap is the last line
            // if a new one slips through: genuine settles need at most ~one pass per slot.
            var maxPasses = (_grid.Columns * _grid.Rows) + 8;
            var passes = 0;
            while (BalanceOnePass(turnSteps, movesOut))
            {
                passes++;
                if (passes >= maxPasses)
                {
                    Debug.LogError(
                        $"BalancePlanner.Plan: no convergence after {passes} passes " +
                        $"({movesOut.Count} moves) — aborting the run. A move cycle slipped past the " +
                        "revisit guard; the board is left mid-settle but consistent.");
                    break;
                }
            }

            ReleaseVisited();
        }

        private bool BalanceOnePass(
            Dictionary<IWriteableDynamicSlotActor, int> turnSteps, List<BalanceMove> movesOut)
        {
            CollectPassCandidates();

            var moved = false;
            for (var i = 0; i < _passCandidates.Count; i++)
            {
                var slot = _passCandidates[i].Slot;
                moved |= TryBalanceSlot(slot.x, slot.y, turnSteps, movesOut);
            }

            return moved;
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
        private bool TryBalanceSlot(
            int col, int row, Dictionary<IWriteableDynamicSlotActor, int> turnSteps, List<BalanceMove> movesOut)
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
            if (stepCap > 0 && turnSteps.GetValueOrDefault(dynamicActor) >= stepCap)
            {
                return false;
            }

            var nextSlot = _balanceQuery.OptimalNextEmptySlot(col, row);
            if (!nextSlot.HasValue)
            {
                return false;
            }

            // Never move an actor back into a slot it already occupied during this Plan: revisiting
            // means the race is cycling, not settling (weight-0 omnidirectional side moves tie with
            // empty-cone up moves and the later-wins tie-break picks them indefinitely). A genuine
            // settle is monotone — it never needs a slot back within the same run.
            if (!_visitedThisPlan.TryGetValue(dynamicActor, out var visited))
            {
                visited = _visitedPool.Count > 0 ? _visitedPool.Pop() : new HashSet<Vector2Int>();
                visited.Add(currentSlot);
                _visitedThisPlan[dynamicActor] = visited;
            }

            if (!visited.Add(nextSlot.Value))
            {
                return false;
            }

            var actorView = _grid.ViewAt(currentSlot);
            _grid.Remove(currentSlot);
            _grid.Place(dynamicActor, actorView, nextSlot.Value);

            movesOut.Add(new BalanceMove(dynamicActor, currentSlot, nextSlot.Value));

            if (stepCap > 0)
            {
                turnSteps[dynamicActor] = turnSteps.GetValueOrDefault(dynamicActor) + 1;
            }

            return true;
        }

        private void ReleaseVisited()
        {
            foreach (var visited in _visitedThisPlan.Values)
            {
                visited.Clear();
                _visitedPool.Push(visited);
            }

            _visitedThisPlan.Clear();
        }

        private struct PassCandidate
        {
            public Vector2Int Slot;
            public int Priority;
            public int Order;
        }
    }

    /// <summary>One executed balance move, in the order <see cref="BalancePlanner.Plan" /> performed it.</summary>
    internal readonly struct BalanceMove
    {
        public readonly IWriteableDynamicSlotActor Actor;
        public readonly Vector2Int From;
        public readonly Vector2Int To;

        public BalanceMove(IWriteableDynamicSlotActor actor, Vector2Int from, Vector2Int to)
        {
            Actor = actor;
            From = from;
            To = to;
        }
    }
}
