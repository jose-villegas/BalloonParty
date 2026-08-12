using System.Collections.Generic;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>One polyline the preview's pens trace, as a range into <see cref="ItemPreviewShape.Points" />.</summary>
    internal readonly struct ItemPreviewStroke
    {
        public readonly int Start;
        public readonly int Count;

        /// <summary>Whether the last point rejoins the first — a circle or rectangle loops, an arm doesn't.</summary>
        public readonly bool Closed;

        public ItemPreviewStroke(int start, int count, bool closed)
        {
            Start = start;
            Count = count;
            Closed = closed;
        }
    }

    /// <summary>
    ///     The figure one item's range draws, as a set of polylines. Reused across rebuilds (cleared, never
    ///     reallocated), so an aim held on an item costs no garbage however often it refreshes.
    /// </summary>
    /// <remarks>
    ///     Points live in one flat list and strokes index into it, rather than a list-of-lists: the ticker
    ///     walks strokes every <c>LateTick</c>, and a jagged structure would chase a pointer per stroke for
    ///     no gain — every figure here is a handful of short polylines.
    /// </remarks>
    internal sealed class ItemPreviewShape
    {
        private readonly List<Vector3> _points = new();
        private readonly List<ItemPreviewStroke> _strokes = new();

        private int _openStart = -1;

        internal IReadOnlyList<Vector3> Points => _points;
        internal IReadOnlyList<ItemPreviewStroke> Strokes => _strokes;

        internal void Clear()
        {
            _points.Clear();
            _strokes.Clear();
            _openStart = -1;
        }

        internal void BeginStroke()
        {
            if (_openStart >= 0)
            {
                Log.Warn("ItemPreviewShape",
                    "BeginStroke called while a stroke is still open — the previous one is closed as-is. " +
                    "An IItemRangePreview must pair every BeginStroke with an EndStroke.");
                EndStroke();
            }

            _openStart = _points.Count;
        }

        internal void AddPoint(Vector3 point)
        {
            if (_openStart < 0)
            {
                Log.Warn("ItemPreviewShape", "AddPoint called outside a stroke — the point is dropped.");
                return;
            }

            _points.Add(point);
        }

        /// <summary>
        ///     Closes the open stroke. A stroke of fewer than two points is discarded rather than recorded:
        ///     it has no length for a pen to travel, and the ticker would divide by its zero arc-length.
        /// </summary>
        internal void EndStroke(bool closed = false)
        {
            if (_openStart < 0)
            {
                return;
            }

            var count = _points.Count - _openStart;
            if (count < 2)
            {
                _points.RemoveRange(_openStart, count);
                _openStart = -1;
                return;
            }

            _strokes.Add(new ItemPreviewStroke(_openStart, count, closed));
            _openStart = -1;
        }

        /// <summary>Appends a closed circle as one stroke — the shape every radius-shaped range reduces to.</summary>
        internal void AddCircle(Vector2 center, float radius, int segments)
        {
            if (radius <= 0f || segments < 3)
            {
                return;
            }

            BeginStroke();
            var step = Mathf.PI * 2f / segments;
            for (var i = 0; i < segments; i++)
            {
                var angle = step * i;
                AddPoint(new Vector3(center.x + (Mathf.Cos(angle) * radius), center.y + (Mathf.Sin(angle) * radius), 0f));
            }

            EndStroke(closed: true);
        }

        /// <summary>Appends a straight two-point stroke.</summary>
        internal void AddSegment(Vector2 from, Vector2 to)
        {
            BeginStroke();
            AddPoint(new Vector3(from.x, from.y, 0f));
            AddPoint(new Vector3(to.x, to.y, 0f));
            EndStroke();
        }
    }
}
