using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Editor-only capture of pressure propagations for <c>BalanceGizmos</c>: a ring of the last
    ///     few resolutions with their nodes, expansion edges and executed chain. Recording methods
    ///     compile away outside the editor, so release builds pay nothing beyond the empty instance.
    /// </summary>
    internal class BalanceDebugRecorder
    {
        internal const int Capacity = 8;

        private readonly List<Resolution> _events = new(Capacity);

        private int _nextSlot;
        private Resolution _current;

        public IReadOnlyList<Resolution> Events => _events;

        [Conditional("UNITY_EDITOR")]
        public void BeginResolution(Vector2Int seed, Vector2 direction)
        {
            if (_events.Count < Capacity)
            {
                _current = new Resolution();
                _events.Add(_current);
            }
            else
            {
                // Reuse the evicted entry's lists — no steady-state allocation growth.
                _current = _events[_nextSlot];
                _current.Nodes.Clear();
                _current.Edges.Clear();
                _current.Chain.Clear();
            }

            _nextSlot = (_nextSlot + 1) % Capacity;
            _current.Timestamp = Time.realtimeSinceStartup;
            _current.Seed = seed;
            _current.SeedDirection = direction;
            _current.Resolved = false;
            _current.Terminal = TerminalKind.EvaluatorMove;
        }

        [Conditional("UNITY_EDITOR")]
        public void RecordNode(Vector2Int slot, Vector2 incomingDirection, float pathScore)
        {
            if (_current == null)
            {
                return;
            }

            _current.Nodes.Add(new NodeRecord(slot, incomingDirection, pathScore, _current.Nodes.Count));
        }

        [Conditional("UNITY_EDITOR")]
        public void RecordEdge(Vector2Int from, Vector2Int to, float alignment, float heaviness, EdgeRejection rejection)
        {
            if (_current == null)
            {
                return;
            }

            _current.Edges.Add(new EdgeRecord(from, to, alignment, heaviness, rejection));
        }

        [Conditional("UNITY_EDITOR")]
        public void RecordMove(Vector2Int from, Vector2Int to)
        {
            if (_current == null)
            {
                return;
            }

            _current.Chain.Add(new MoveRecord(from, to));
        }

        [Conditional("UNITY_EDITOR")]
        public void RecordTerminal(TerminalKind kind)
        {
            if (_current == null)
            {
                return;
            }

            _current.Terminal = kind;
            _current.Resolved = true;
        }

        internal enum TerminalKind
        {
            EvaluatorMove,
            Relocation,
        }

        internal enum EdgeRejection
        {
            None,
            Static,
            Visited,
            Backflow,
        }

        internal sealed class Resolution
        {
            internal readonly List<NodeRecord> Nodes = new();
            internal readonly List<EdgeRecord> Edges = new();
            internal readonly List<MoveRecord> Chain = new();

            internal float Timestamp;
            internal Vector2Int Seed;
            internal Vector2 SeedDirection;
            internal bool Resolved;
            internal TerminalKind Terminal;
        }

        internal readonly struct NodeRecord
        {
            public readonly Vector2Int Slot;
            public readonly Vector2 IncomingDirection;
            public readonly float PathScore;
            public readonly int Order;

            public NodeRecord(Vector2Int slot, Vector2 incomingDirection, float pathScore, int order)
            {
                Slot = slot;
                IncomingDirection = incomingDirection;
                PathScore = pathScore;
                Order = order;
            }
        }

        internal readonly struct EdgeRecord
        {
            public readonly Vector2Int From;
            public readonly Vector2Int To;
            public readonly float Alignment;
            public readonly float Heaviness;
            public readonly EdgeRejection Rejection;

            public bool Accepted => Rejection == EdgeRejection.None;

            public EdgeRecord(Vector2Int from, Vector2Int to, float alignment, float heaviness, EdgeRejection rejection)
            {
                From = from;
                To = to;
                Alignment = alignment;
                Heaviness = heaviness;
                Rejection = rejection;
            }
        }

        internal readonly struct MoveRecord
        {
            public readonly Vector2Int From;
            public readonly Vector2Int To;

            public MoveRecord(Vector2Int from, Vector2Int to)
            {
                From = from;
                To = to;
            }
        }
    }
}
