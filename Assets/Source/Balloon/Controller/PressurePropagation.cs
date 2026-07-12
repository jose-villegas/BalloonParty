using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Directed, weighted pressure search: a board-up shove enters at a blocked column's lowest
    ///     blocker and propagates through occupied movables — bending toward each shoved neighbour —
    ///     until an actor can vacate into an empty slot. Heavier movers cost more per hop, so lighter
    ///     chains are explored (and chosen) first. If the directed pass exhausts without a mover, a
    ///     single undirected retry guarantees any reachable pocket still gets filled.
    /// </summary>
    internal class PressurePropagation
    {
        // A perfectly aligned hop through a 1-step heavy scores below a half-aligned hop through a light.
        internal const float HeavinessPenalty = 0.75f;

        private readonly SlotGrid _grid;
        private readonly MoveWeightEvaluator _evaluator;
        private readonly BalanceDebugRecorder _recorder;

        // Reused across calls, cleared at entry — this runs during overflow crunch.
        private readonly List<Node> _nodes = new();
        private readonly List<int> _open = new();
        private readonly HashSet<Vector2Int> _visited = new();
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];

        private bool _relaxed;

        public PressurePropagation(SlotGrid grid, MoveWeightEvaluator evaluator, BalanceDebugRecorder recorder = null)
        {
            _grid = grid;
            _evaluator = evaluator;
            _recorder = recorder;
        }

        /// <summary>
        ///     Finds the shove chain that frees <paramref name="startColumn" />'s entry, filling
        ///     <paramref name="moves" /> ordered from the mover backward to the seed so every destination
        ///     is empty at execution time. False when no chain exists.
        /// </summary>
        public bool TryResolve(int startColumn, List<Move> moves)
        {
            moves.Clear();

            // The first non-traversable occupant is what must move, not the literal bottom cell.
            if (!TryFindLowestBlocker(startColumn, out var seed))
            {
                return false;
            }

            var seedOccupant = _grid.At(seed);
            if (seedOccupant is not IPressureMovable
                || seedOccupant is not IWriteableDynamicSlotActor seedActor)
            {
                return false;
            }

            if (RunPass(seed, seedActor, moves, relaxed: false))
            {
                return true;
            }

            // A failed resolve charges the player a hit point, so the shove direction is a preference
            // for which path wins, never a reason to lose a placement: when the strict pass strands a
            // reachable pocket behind the shove (e.g. the only gap sits below the seed), retry once
            // undirected — alignment dropped from gating and scoring, heaviness costs kept.
            return RunPass(seed, seedActor, moves, relaxed: true);
        }

        private bool RunPass(Vector2Int seed, IWriteableDynamicSlotActor seedActor, List<Move> moves, bool relaxed)
        {
            _relaxed = relaxed;
            _nodes.Clear();
            _open.Clear();
            _visited.Clear();

            _visited.Add(seed);
            _nodes.Add(new Node
            {
                Slot = seed,
                Shove = new ShoveVector(Vector2.up, seed),
                Actor = seedActor,
                Parent = -1,
            });
            _open.Add(0);

            RecordBegin(seed);

            while (_open.Count > 0)
            {
                var nodeIndex = PopBest();
                var node = _nodes[nodeIndex];

                RecordNode(in node);

                // Relocating actors are terminals: they vacate to a free slot and the chain ends here.
                var response = ((IPressureMovable)node.Actor).PushResponse;
                if (response != PressureResponse.ShoveNeighbour
                    && TryRelocationTarget(node.Slot, response, out var destination))
                {
                    RecordTerminal(BalanceDebugRecorder.TerminalKind.Relocation);
                    EmitMoves(nodeIndex, destination, moves);
                    return true;
                }

                if (TryFindEscape(in node, out var escape))
                {
                    RecordTerminal(BalanceDebugRecorder.TerminalKind.EvaluatorMove);
                    EmitMoves(nodeIndex, escape, moves);
                    return true;
                }

                Expand(nodeIndex);
            }

            return false;
        }

        private bool TryFindEscape(in Node node, out Vector2Int escape)
        {
            if (!_relaxed)
            {
                var best = _evaluator.BestMove(node.Slot.x, node.Slot.y, node.Shove);
                escape = best ?? default;
                return best.HasValue;
            }

            // Undirected pass: any empty neighbour is an escape. A per-hop shove aimed straight at
            // the candidate keeps the evaluator's side/down gate open (and adds the same aligned
            // pressure term to every candidate), so its support weights still rank the ties.
            var from = node.Slot;
            var origin = _grid.IndexToWorldPosition(from);
            HexCoordinates.HexNeighborIndices(from.x, from.y, _neighborBuffer);

            escape = default;
            var bestWeight = int.MinValue;
            var found = false;

            foreach (var candidate in _neighborBuffer)
            {
                if (!_grid.InBounds(candidate) || !_grid.IsEmpty(candidate.x, candidate.y))
                {
                    continue;
                }

                var direction = ((Vector2)(_grid.IndexToWorldPosition(candidate) - origin)).normalized;
                var shove = new ShoveVector(direction, from);
                // >= keeps BestMove's historical tie-break: the later buffer entry wins equal scores.
                if (_evaluator.TryScoreMove(from, candidate, shove, out var weight) && weight >= bestWeight)
                {
                    bestWeight = weight;
                    escape = candidate;
                    found = true;
                }
            }

            return found;
        }

        // Occupied movable neighbours join the frontier; the shove bends to point from shover to shoved.
        private void Expand(int nodeIndex)
        {
            var node = _nodes[nodeIndex];
            var origin = _grid.IndexToWorldPosition(node.Slot);
            HexCoordinates.HexNeighborIndices(node.Slot.x, node.Slot.y, _neighborBuffer);

            foreach (var neighbour in _neighborBuffer)
            {
                if (!_grid.InBounds(neighbour) || _grid.IsEmpty(neighbour.x, neighbour.y))
                {
                    continue;
                }

                if (_visited.Contains(neighbour))
                {
                    RecordEdge(node.Slot, neighbour, 0f, 0f, BalanceDebugRecorder.EdgeRejection.Visited);
                    continue;
                }

                var occupant = _grid.At(neighbour);
                if (occupant is not IPressureMovable
                    || occupant is not IWriteableDynamicSlotActor dynamicOccupant)
                {
                    RecordEdge(node.Slot, neighbour, 0f, 0f, BalanceDebugRecorder.EdgeRejection.Static);
                    continue;
                }

                var direction = ((Vector2)(_grid.IndexToWorldPosition(neighbour) - origin)).normalized;
                var alignment = Vector2.Dot(direction, node.Shove.Direction);
                if (!_relaxed && alignment < 0f)
                {
                    // Never shove back toward where the shove came from.
                    RecordEdge(node.Slot, neighbour, alignment, 0f, BalanceDebugRecorder.EdgeRejection.Backflow);
                    continue;
                }

                var heaviness = Heaviness(occupant);
                _visited.Add(neighbour);
                _nodes.Add(new Node
                {
                    Slot = neighbour,
                    Shove = new ShoveVector(direction, node.Slot),
                    Actor = dynamicOccupant,
                    Parent = nodeIndex,
                    PathScore = node.PathScore + (_relaxed ? 0f : alignment) - heaviness,
                });
                _open.Add(_nodes.Count - 1);

                RecordEdge(node.Slot, neighbour, alignment, heaviness, BalanceDebugRecorder.EdgeRejection.None);
            }
        }

        // Best cumulative path score wins; ties keep insertion order (node indices are insertion-ordered).
        private int PopBest()
        {
            var bestOpen = 0;
            var bestScore = _nodes[_open[0]].PathScore;
            for (var i = 1; i < _open.Count; i++)
            {
                var score = _nodes[_open[i]].PathScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOpen = i;
                }
            }

            var nodeIndex = _open[bestOpen];
            _open.RemoveAt(bestOpen);
            return nodeIndex;
        }

        // Walks the parent chain from the vacating tip back to the seed: each ancestor steps into the
        // slot its child just left, so executing in list order always lands on an empty cell.
        private void EmitMoves(int tipIndex, Vector2Int target, List<Move> moves)
        {
            var destination = target;
            for (var index = tipIndex; index >= 0; index = _nodes[index].Parent)
            {
                var node = _nodes[index];
                moves.Add(new Move(node.Actor, node.Slot, destination));
                RecordMove(node.Slot, destination);
                destination = node.Slot;
            }
        }

        // The first non-traversable occupant walking up from the entry.
        private bool TryFindLowestBlocker(int col, out Vector2Int blocker)
        {
            for (var row = _grid.Rows - 1; row >= 0; row--)
            {
                if (!_grid.IsEmpty(col, row) && !_grid.IsTraversable(col, row))
                {
                    blocker = new Vector2Int(col, row);
                    return true;
                }
            }

            blocker = default;
            return false;
        }

        private bool TryRelocationTarget(Vector2Int from, PressureResponse response, out Vector2Int target)
        {
            target = default;
            var farthest = response == PressureResponse.RelocateFarthest;
            var best = farthest ? int.MinValue : int.MaxValue;
            var found = false;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (!_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var dx = col - from.x;
                    var dy = row - from.y;
                    var distance = (dx * dx) + (dy * dy);

                    var better = farthest ? distance > best : distance < best;
                    if (better)
                    {
                        best = distance;
                        target = new Vector2Int(col, row);
                        found = true;
                    }
                }
            }

            return found;
        }

        // MaxBalanceSteps never gates under pressure; it only prices the hop (0/absent = weightless).
        private static float Heaviness(IWriteableSlotActor occupant)
        {
            var steps = (occupant as IBalanceInfluence)?.MaxBalanceSteps ?? 0;
            return steps <= 0 ? 0f : HeavinessPenalty / steps;
        }

        // Conditional wrappers keep the search body clean; release builds elide the call sites entirely.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RecordBegin(Vector2Int seed)
        {
            if (_recorder != null)
            {
                _recorder.BeginResolution(seed, Vector2.up);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RecordNode(in Node node)
        {
            if (_recorder != null)
            {
                _recorder.RecordNode(node.Slot, node.Shove.Direction, node.PathScore);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RecordEdge(
            Vector2Int from,
            Vector2Int to,
            float alignment,
            float heaviness,
            BalanceDebugRecorder.EdgeRejection rejection)
        {
            if (_recorder != null)
            {
                _recorder.RecordEdge(from, to, alignment, heaviness, rejection);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RecordMove(Vector2Int from, Vector2Int to)
        {
            if (_recorder != null)
            {
                _recorder.RecordMove(from, to);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RecordTerminal(BalanceDebugRecorder.TerminalKind kind)
        {
            if (_recorder != null)
            {
                _recorder.RecordTerminal(kind);
            }
        }

        internal readonly struct Move
        {
            public readonly IWriteableDynamicSlotActor Actor;
            public readonly Vector2Int From;
            public readonly Vector2Int To;

            public Move(IWriteableDynamicSlotActor actor, Vector2Int from, Vector2Int to)
            {
                Actor = actor;
                From = from;
                To = to;
            }
        }

        private struct Node
        {
            public Vector2Int Slot;
            public ShoveVector Shove;
            public IWriteableDynamicSlotActor Actor;
            public int Parent;
            public float PathScore;
        }
    }
}
