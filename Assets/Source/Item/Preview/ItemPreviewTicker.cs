using System;
using System.Collections.Generic;
using BalloonParty.Shared.Pool;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Drives the pens of the one visible item-range figure, closed-form off a single clock — no tweens,
    ///     no coroutines, no per-frame allocation, mirroring <c>ShapeFormationTicker</c>'s constraints.
    /// </summary>
    /// <remarks>
    ///     Exactly one figure is ever shown, so this owns a single pen set that <see cref="Show" /> re-aims
    ///     rather than a registry of concurrent previews. Arbitration of WHICH host is sighted belongs to
    ///     <see cref="ItemRangePreviewController" />.
    /// </remarks>
    internal sealed class ItemPreviewTicker : ILateTickable, IDisposable
    {
        private readonly PoolManager _poolManager;
        private readonly HighlightTrail _penPrefab;
        private readonly ItemPreviewSettings _settings;
        private readonly string _poolKey;

        private readonly ItemPreviewShape _shape = new();
        private readonly List<Pen> _pens = new();

        // Cumulative arc length per stroke, flattened: _arcTable[stroke.Start + i] is the distance from the
        // stroke's first point to its i-th. Rebuilt per Show, sized to the shape's own point count.
        private readonly List<float> _arcTable = new();
        private readonly List<float> _strokeLengths = new();

        // How many pens have been dealt to each stroke so far this AcquirePens pass — a reused counter
        // rather than a per-call dictionary, so re-aiming at a new host allocates nothing.
        private readonly List<int> _pensPerStroke = new();

        private Vector2 _origin;
        private Vector2Int _currentSlot;
        private bool _visible;

        internal ItemPreviewTicker(
            PoolManager poolManager, HighlightTrail penPrefab, ItemPreviewSettings settings)
        {
            _poolManager = poolManager;
            _penPrefab = penPrefab;
            _settings = settings;
            _poolKey = penPrefab != null ? penPrefab.name : nameof(HighlightTrail);
        }

        public void Dispose()
        {
            Hide();
        }

        /// <summary>
        ///     Builds <paramref name="preview" />'s figure for this crossing and aims the pens at it.
        /// </summary>
        /// <remarks>
        ///     Called on every aim change while a host stays sighted, so it distinguishes the two cases by
        ///     <see cref="ItemPreviewContext.Slot" />: a DIFFERENT host restarts the pens (they re-bloom out
        ///     of the new origin), while the SAME host only re-fits the geometry — the figure follows a
        ///     drifting balloon, or a Shield plus follows the aim tip, without every pen snapping back to the
        ///     start of its bloom each time the player nudges the aim.
        /// </remarks>
        internal void Show(IItemRangePreview preview, in ItemPreviewContext context, Color color)
        {
            if (preview == null || _penPrefab == null)
            {
                Hide();
                return;
            }

            _shape.Clear();
            preview.BuildShape(in context, _shape);

            // An item with no board figure (or one whose geometry degenerated this frame) shows nothing
            // rather than stranding pens at the host.
            if (_shape.Strokes.Count == 0)
            {
                Hide();
                return;
            }

            var isSameHost = _visible && context.Slot == _currentSlot;
            _origin = context.Origin;
            _currentSlot = context.Slot;
            BuildArcTable();

            if (isSameHost)
            {
                RefitPens();
            }
            else
            {
                AcquirePens(color);
            }

            _visible = true;
        }

        internal void Hide()
        {
            if (!_visible && _pens.Count == 0)
            {
                return;
            }

            for (var i = 0; i < _pens.Count; i++)
            {
                var trail = _pens[i].Trail;
                if (trail != null)
                {
                    _poolManager.Return(_poolKey, trail);
                }
            }

            _pens.Clear();
            _visible = false;
        }

        public void LateTick()
        {
            if (!_visible)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            for (var i = 0; i < _pens.Count; i++)
            {
                var pen = _pens[i];
                AdvancePen(ref pen, deltaTime);
                _pens[i] = pen;
            }
        }

        // Cumulative arc length per point, so a pen's travel can be expressed as one distance that maps to a
        // position without walking the whole stroke each frame from scratch.
        private void BuildArcTable()
        {
            _arcTable.Clear();
            _strokeLengths.Clear();

            var points = _shape.Points;
            for (var i = 0; i < points.Count; i++)
            {
                _arcTable.Add(0f);
            }

            for (var s = 0; s < _shape.Strokes.Count; s++)
            {
                var stroke = _shape.Strokes[s];
                var total = 0f;
                _arcTable[stroke.Start] = 0f;

                for (var i = 1; i < stroke.Count; i++)
                {
                    total += Vector3.Distance(points[stroke.Start + i - 1], points[stroke.Start + i]);
                    _arcTable[stroke.Start + i] = total;
                }

                // A closed stroke's last leg rejoins point 0 and is not in the table — carry it in the
                // length alone, and SampleStroke wraps into it past the final tabled point.
                if (stroke.Closed)
                {
                    total += Vector3.Distance(points[stroke.Start + stroke.Count - 1], points[stroke.Start]);
                }

                _strokeLengths.Add(total);
            }
        }

        // Pens are dealt round-robin across strokes, so a two-armed figure splits them evenly without any
        // item needing to state how many it wants; within a stroke they start evenly spaced along its length.
        private void AcquirePens(Color color)
        {
            var wanted = Mathf.Max(1, _settings.PenCount);

            while (_pens.Count > wanted)
            {
                var last = _pens.Count - 1;
                var trail = _pens[last].Trail;
                if (trail != null)
                {
                    _poolManager.Return(_poolKey, trail);
                }

                _pens.RemoveAt(last);
            }

            while (_pens.Count < wanted)
            {
                _pens.Add(new Pen());
            }

            var strokeCount = _shape.Strokes.Count;
            var pensOnStroke = Mathf.Max(1, Mathf.CeilToInt(_pens.Count / (float)strokeCount));

            _pensPerStroke.Clear();
            for (var s = 0; s < strokeCount; s++)
            {
                _pensPerStroke.Add(0);
            }

            for (var i = 0; i < _pens.Count; i++)
            {
                var pen = _pens[i];
                pen.Trail ??= _poolManager.GetOrRegister(
                    _poolKey, () => new SimplePoolChannel<HighlightTrail>(_penPrefab));
                pen.StrokeIndex = i % strokeCount;

                // Spread this stroke's pens evenly over its length, by how many have already landed on it.
                var ordinal = _pensPerStroke[pen.StrokeIndex];
                _pensPerStroke[pen.StrokeIndex] = ordinal + 1;

                var length = _strokeLengths[pen.StrokeIndex];
                pen.EntryDistance = length * (ordinal / (float)pensOnStroke);
                pen.Distance = pen.EntryDistance;
                pen.Entry = SampleStroke(pen.StrokeIndex, pen.EntryDistance);
                pen.BloomElapsed = 0f;
                pen.Tracing = false;

                pen.Trail.SetColor(color);
                pen.Trail.ClearRibbon();
                pen.Trail.SetPosition(new Vector3(_origin.x, _origin.y, 0f));
                pen.Trail.SetEmitting(_settings.EmitDuringBloom);

                _pens[i] = pen;
            }
        }

        // Same host, re-fitted geometry: keep every pen's phase and progress, only clamp what the new stroke
        // set can no longer support. A pen still blooming re-aims at the moved entry point mid-flight.
        private void RefitPens()
        {
            var strokeCount = _shape.Strokes.Count;

            for (var i = 0; i < _pens.Count; i++)
            {
                var pen = _pens[i];
                if (pen.StrokeIndex >= strokeCount)
                {
                    pen.StrokeIndex = i % strokeCount;
                }

                pen.Entry = SampleStroke(pen.StrokeIndex, pen.EntryDistance);
                _pens[i] = pen;
            }
        }

        private void AdvancePen(ref Pen pen, float deltaTime)
        {
            if (pen.Trail == null)
            {
                return;
            }

            if (!pen.Tracing)
            {
                AdvanceBloom(ref pen, deltaTime);
                return;
            }

            pen.Distance += _settings.TraceSpeed * deltaTime;
            pen.Trail.SetPosition(SampleStroke(pen.StrokeIndex, pen.Distance));
        }

        // The outward launch: the pen spirals from the host to its stroke entry — bearing eases from an
        // offset launch angle onto the true one while the radius grows, so it arcs out rather than shooting
        // straight, and still lands exactly on the entry point at t = 1.
        private void AdvanceBloom(ref Pen pen, float deltaTime)
        {
            var duration = Mathf.Max(_settings.BloomDuration, 1e-4f);
            pen.BloomElapsed += deltaTime;
            var t = Mathf.Clamp01(pen.BloomElapsed / duration);
            var eased = Mathf.SmoothStep(0f, 1f, t);

            var toEntry = new Vector2(pen.Entry.x - _origin.x, pen.Entry.y - _origin.y);
            var targetAngle = Mathf.Atan2(toEntry.y, toEntry.x) * Mathf.Rad2Deg;
            var angle = Mathf.LerpAngle(targetAngle - _settings.BloomSweepDegrees, targetAngle, eased);
            var radius = Mathf.Lerp(0f, toEntry.magnitude, eased);
            var radians = angle * Mathf.Deg2Rad;

            pen.Trail.SetPosition(new Vector3(
                _origin.x + (Mathf.Cos(radians) * radius),
                _origin.y + (Mathf.Sin(radians) * radius),
                0f));

            if (t < 1f)
            {
                return;
            }

            pen.Tracing = true;

            // Pen down for the figure itself. When the bloom drew too, the spiral in and the stroke are one
            // continuous ribbon; when it didn't, clearing here drops the invisible approach so the first
            // traced segment doesn't join back to the host.
            if (!_settings.EmitDuringBloom)
            {
                pen.Trail.ClearRibbon();
                pen.Trail.SetEmitting(true);
            }
        }

        // Distance wraps: a closed stroke loops forever, an open one ping-pongs so a pen never stalls at
        // an end or jumps back across the figure.
        private Vector3 SampleStroke(int strokeIndex, float distance)
        {
            var stroke = _shape.Strokes[strokeIndex];
            var points = _shape.Points;
            var length = _strokeLengths[strokeIndex];

            if (length <= 1e-5f)
            {
                return points[stroke.Start];
            }

            if (stroke.Closed)
            {
                distance = Mathf.Repeat(distance, length);
            }
            else
            {
                distance = Mathf.PingPong(distance, length);
            }

            // The tabled points cover [0, arc(last)]; past that only a closed stroke's wrap leg remains.
            var lastIndex = stroke.Start + stroke.Count - 1;
            if (distance >= _arcTable[lastIndex])
            {
                if (!stroke.Closed)
                {
                    return points[lastIndex];
                }

                var legLength = length - _arcTable[lastIndex];
                var legT = legLength <= 1e-5f ? 0f : (distance - _arcTable[lastIndex]) / legLength;
                return Vector3.Lerp(points[lastIndex], points[stroke.Start], legT);
            }

            for (var i = stroke.Start + 1; i <= lastIndex; i++)
            {
                if (distance > _arcTable[i])
                {
                    continue;
                }

                var segmentLength = _arcTable[i] - _arcTable[i - 1];
                var segmentT = segmentLength <= 1e-5f ? 0f : (distance - _arcTable[i - 1]) / segmentLength;
                return Vector3.Lerp(points[i - 1], points[i], segmentT);
            }

            return points[lastIndex];
        }

        private struct Pen
        {
            public HighlightTrail Trail;
            public int StrokeIndex;
            public float BloomElapsed;
            public float Distance;
            public float EntryDistance;
            public Vector3 Entry;
            public bool Tracing;
        }
    }
}
