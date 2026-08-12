using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
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
        private readonly IItemPreviewConfig _config;
        private readonly string _poolKey;

        private readonly ItemPreviewShape _shape = new();
        private readonly List<Pen> _pens = new();

        // Cumulative arc length per stroke, flattened: _arcTable[stroke.Start + i] is the distance from the
        // stroke's first point to its i-th. Rebuilt per Show, sized to the shape's own point count.
        private readonly List<float> _arcTable = new();
        private readonly List<float> _strokeLengths = new();

        // Dashes derived per stroke this Show pass — sized to stroke count and reused rather than
        // reallocated, so re-aiming at a new host allocates nothing. Doubles as the pens-per-stroke count,
        // since dashing is universal now: one pen per dash, by definition.
        private readonly List<int> _dashesPerStroke = new();

        private Vector2 _origin;
        private Vector2Int _currentSlot;
        private bool _visible;

        // Captured on Show so LateTick doesn't re-resolve the config every frame.
        private float _dashLength;
        private bool _emitDuringBloom;

        internal ItemPreviewTicker(
            PoolManager poolManager, HighlightTrail penPrefab, IItemPreviewConfig config)
        {
            _poolManager = poolManager;
            _penPrefab = penPrefab;
            _config = config;
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
        ///     drifting balloon, or a Shield stub follows the aim tip, without every pen snapping back to
        ///     the start of its bloom each time the player nudges the aim.
        ///     <para>
        ///         Carries no colour: every figure draws with the pen prefab's own material, so the
        ///         telegraph reads as one system and there is no runtime tint path to keep in step with it.
        ///     </para>
        /// </remarks>
        internal void Show(IItemRangePreview preview, in ItemPreviewContext context)
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
            var style = _config.StyleFor(preview.Type);
            _dashLength = _config.DashLength;
            _emitDuringBloom = style.BloomDraw switch
            {
                ItemPreviewBloomDraw.Draw => true,
                ItemPreviewBloomDraw.Hide => false,
                _ => _config.EmitDuringBloom
            };
            _origin = context.Origin;
            _currentSlot = context.Slot;
            BuildArcTable();

            if (isSameHost)
            {
                RefitPens();
            }
            else
            {
                AcquirePens(style);
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

        // Each stroke derives its own dash count from its own length, so a long stroke gets more dashes
        // than a short one instead of every figure sharing one authored count — that count IS the
        // pens-on-that-stroke, one pen per dash, by definition.
        //
        // Laser is why the cap below is mandatory rather than a nice-to-have: its two ~40-unit corridors
        // sum to roughly 160 units of stroke length, which at the authored stride would derive around
        // 320 pens — 320 pooled TrailRenderers for one figure. Past MaxPens the stride is inflated once
        // and every stroke recomputed with it, so a big figure's dashes get sparser and longer-spaced
        // rather than any part of it going undrawn or the pool blowing out.
        private void AcquirePens(IItemPreviewStyle style)
        {
            var strokeCount = _shape.Strokes.Count;
            var stride = _config.DashLength + _config.DashSpacing;

            _dashesPerStroke.Clear();
            var desiredTotal = 0;
            for (var s = 0; s < strokeCount; s++)
            {
                var count = stride <= 1e-5f ? 1 : Mathf.Max(1, Mathf.RoundToInt(_strokeLengths[s] / stride));
                _dashesPerStroke.Add(count);
                desiredTotal += count;
            }

            if (desiredTotal > _config.MaxPens && stride > 1e-5f)
            {
                var inflatedStride = stride * (desiredTotal / (float)_config.MaxPens);
                desiredTotal = 0;
                for (var s = 0; s < strokeCount; s++)
                {
                    var count = Mathf.Max(1, Mathf.RoundToInt(_strokeLengths[s] / inflatedStride));
                    _dashesPerStroke[s] = count;
                    desiredTotal += count;
                }
            }

            var wanted = desiredTotal;

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

            var penIndex = 0;
            for (var s = 0; s < strokeCount; s++)
            {
                var dashesOnStroke = _dashesPerStroke[s];
                for (var dashIndex = 0; dashIndex < dashesOnStroke; dashIndex++)
                {
                    var pen = _pens[penIndex];
                    pen.Trail ??= _poolManager.GetOrRegister(
                        _poolKey, () => new SimplePoolChannel<HighlightTrail>(_penPrefab));
                    pen.StrokeIndex = s;
                    pen.DashIndex = dashIndex;

                    // Distance is measured inside the pen's own slot, so it always starts at 0 — the
                    // slot's offset along the stroke is applied when sampling, in AdvanceDash.
                    pen.Distance = 0f;
                    pen.BloomElapsed = 0f;
                    pen.Bloomed = false;

                    // Spread over the whole pen set (global penIndex), not per-stroke (dashIndex) — pens
                    // are dealt stroke by stroke, so a per-stroke phase would clump the fan instead of
                    // spreading it evenly around the full circle.
                    pen.BloomPhaseDegrees = 360f * penIndex / _pens.Count;

                    pen.Trail.SetRibbonTime(style.RibbonSeconds);
                    pen.Trail.ClearRibbon();
                    pen.Trail.SetPosition(new Vector3(_origin.x, _origin.y, 0f));
                    pen.Trail.SetEmitting(_emitDuringBloom);

                    _pens[penIndex] = pen;
                    penIndex++;
                }
            }
        }

        // Same host, re-fitted geometry: keep every pen's phase and progress, only clamp what the new stroke
        // set can no longer support. A pen still blooming needs no re-aim here — the warp re-derives from
        // the live shape position every frame in AdvancePen, so it just follows the refitted geometry.
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

                _pens[i] = pen;
            }
        }

        // The pen's on-screen position every frame: the shape position it would have with no bloom at all
        // (already advancing from t = 0), warped around the host origin by a clock that decays to identity.
        // At t = 0 the warp collapses the pen onto the origin; at t = 1 rotation is zero and scale is one,
        // so the warp IS the shape position — one continuous formula rather than a spiral phase handing off
        // to a separate tracing phase, so there is no velocity discontinuity at the seam.
        private void AdvancePen(ref Pen pen, float deltaTime)
        {
            if (pen.Trail == null)
            {
                return;
            }

            var shapePos = AdvanceDash(ref pen, deltaTime);

            var duration = Mathf.Max(_config.BloomDuration, 1e-4f);
            pen.BloomElapsed += deltaTime;
            var t = Mathf.Clamp01(pen.BloomElapsed / duration);

            if (t >= 1f)
            {
                pen.Trail.SetPosition(shapePos);

                // Pen down for the figure itself, fired once on the frame the warp first settles. When the
                // bloom drew too, the spiral in and the stroke are one continuous ribbon; when it didn't,
                // clearing here drops the invisible approach so the first traced segment doesn't join back
                // to the host.
                if (!pen.Bloomed)
                {
                    pen.Bloomed = true;

                    if (!_emitDuringBloom)
                    {
                        pen.Trail.ClearRibbon();
                        pen.Trail.SetEmitting(true);
                    }
                }

                return;
            }

            var eased = _config.BloomCurve.Evaluate(t);
            var offsetX = shapePos.x - _origin.x;
            var offsetY = shapePos.y - _origin.y;
            var radians = (_config.BloomSweepDegrees + pen.BloomPhaseDegrees) * (1f - eased) * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            var rotatedX = (offsetX * cos) - (offsetY * sin);
            var rotatedY = (offsetX * sin) + (offsetY * cos);

            pen.Trail.SetPosition(new Vector3(
                _origin.x + (rotatedX * eased),
                _origin.y + (rotatedY * eased),
                0f));
        }

        // One pen draws ONE dash, and the dashed line is the pens sitting next to each other — ask for
        // three dashes and you get three pens, each owning a third of the stroke. Dashing is the only
        // drawing style now: zero spacing (DashSpacing == 0) collapses a slot to exactly DashLength, so
        // painted == slotLength below and adjacent dashes touch with no gap — a solid line falls out of
        // this same code rather than needing a separate continuous-mode branch.
        //
        // Within its own slot a pen loops: it paints for DashLength (the dash), lifts for the remainder
        // (the spacing), and wraps back to its slot start to redraw. Pen up/down via emitting, never
        // ClearRibbon — clearing wipes what was already painted, which is what made an earlier attempt
        // read as one short stroke sliding along the figure instead of a dashed line.
        //
        // A pen never leaves its slot, so the whole figure is always described at once rather than being
        // revealed by a pen touring it.
        private Vector3 AdvanceDash(ref Pen pen, float deltaTime)
        {
            var slotLength = _strokeLengths[pen.StrokeIndex] / _dashesPerStroke[pen.StrokeIndex];
            if (slotLength <= 1e-5f)
            {
                return SampleStroke(pen.StrokeIndex, 0f);
            }

            // The pen SWEEPS its dash — a → b, then b → a, forever. It never jumps, so there is no
            // restart to flicker, no discontinuity to hide, and no pen-up at all: the spacing between
            // dashes is simply arc no pen ever visits (DashLength < the slot it owns).
            //
            // The earlier a → b, snap-back-to-a, repeat is what strobed: every snap ended one ribbon and
            // began another, so the ribbon lifetime decided how many stale copies piled up behind it.
            var painted = Mathf.Min(_dashLength, slotLength);
            pen.Distance += _config.TraceSpeed * deltaTime;

            var withinDash = Mathf.PingPong(pen.Distance, painted);
            return SampleStroke(pen.StrokeIndex, (pen.DashIndex * slotLength) + withinDash);
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

            // Arc length travelled inside this pen's OWN slot, wrapped by the slot length.
            public float Distance;
            public bool Bloomed;

            // The slot this pen owns for its whole life. One pen draws one dash — the dashed line is the
            // pens sitting side by side, not one pen visiting every slot.
            public int DashIndex;

            // Evenly spaced launch bearings so the set fans out radially on bloom instead of turning as
            // one rigid stick; decays away with the shared sweep, so it never moves the landing position.
            public float BloomPhaseDegrees;
        }
    }
}
