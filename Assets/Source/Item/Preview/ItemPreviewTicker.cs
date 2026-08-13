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
        // How many multiples of one frame's worth of sweep travel (TraceSpeed * deltaTime) count as a
        // teleport rather than normal motion. Exists to absorb frame-time spikes, not to tune a look —
        // raise it if a slow device's hitches start reading as false teleports, not for visual taste.
        private const float TeleportSpeedMultiplier = 2f;

        // Floor on the teleport distance threshold itself, so a near-zero deltaTime frame (e.g. the
        // first tick after a stall) can't collapse the threshold to ~0 and flag ordinary motion.
        private const float MinTeleportDistance = 0.01f;

        private readonly PoolManager _poolManager;
        private readonly HighlightTrail _penPrefab;
        private readonly IItemPreviewConfig _config;
        private readonly ItemPreviewViewport _viewport;
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

        // A graceful hide stops emitting but keeps the pens where they are, so what was already drawn
        // fades out over the ribbon's own lifetime instead of vanishing. _fadeDuration is that lifetime,
        // read off a pen's trail at BeginHide rather than authored again here (see
        // HighlightTrail.EffectiveRibbonSeconds); _fadeElapsed tracks how far into it LateTick is.
        private bool _fading;
        private float _fadeElapsed;
        private float _fadeDuration;

        // Captured on Show so LateTick doesn't re-resolve the config every frame.
        private float _dashSpacing;
        private bool _emitDuringBloom;

        internal ItemPreviewTicker(
            PoolManager poolManager,
            HighlightTrail penPrefab,
            IItemPreviewConfig config,
            ItemPreviewViewport viewport)
        {
            _poolManager = poolManager;
            _penPrefab = penPrefab;
            _config = config;
            _viewport = viewport;
            _poolKey = penPrefab != null ? penPrefab.name : nameof(HighlightTrail);
        }

        public void Dispose()
        {
            Hide();
        }

        /// <summary>
        ///     Builds <paramref name="preview" />'s figure for this crossing and aims the pens at it.
        /// </summary>
        /// <param name="introduce">
        ///     True for a figure's first appearance on this host — pens are acquired at the host origin and
        ///     the outward bloom plays, exactly as before this parameter existed. False for a re-settle on
        ///     a host already being telegraphed (the aim nudged but landed on the same host): pens acquired
        ///     here start already in their settled, post-bloom state, so the figure reappears in place
        ///     instead of spiralling back in. <see cref="ItemRangePreviewController" /> decides which this
        ///     is by tracking the slot it last actually showed.
        /// </param>
        /// <remarks>
        ///     Called on every aim change while a host stays sighted, so it distinguishes the two cases by
        ///     <see cref="ItemPreviewContext.Slot" />: a DIFFERENT host restarts the pens (they re-bloom out
        ///     of the new origin), while the SAME host only re-fits the geometry — the figure follows a
        ///     drifting balloon, or a Shield stub follows the aim tip, without every pen snapping back to
        ///     the start of its bloom each time the player nudges the aim. In current play the controller
        ///     never calls this on the same-host-still-visible path (it only re-`Show`s after a
        ///     <see cref="BeginHide" />, which always drops <c>_visible</c> first), so that branch is a
        ///     dormant fallback; <paramref name="introduce" /> is the mechanism that actually distinguishes
        ///     the two cases in practice, independently of whether the geometry itself is acquired or
        ///     re-fitted.
        ///     <para>
        ///         Carries no colour: every figure draws with the pen prefab's own material, so the
        ///         telegraph reads as one system and there is no runtime tint path to keep in step with it.
        ///     </para>
        /// </remarks>
        internal void Show(IItemRangePreview preview, in ItemPreviewContext context, bool introduce)
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

            // A Show arriving mid-fade supersedes it outright — cancelling here (rather than letting
            // LateTick's fade timer run) stops it from releasing these pens later while they're already
            // reused for the figure being built below. _visible is already false from BeginHide, so
            // isSameHost comes out false regardless of slot, which is what routes this into AcquirePens
            // instead of RefitPens: the acquire path is what re-blooms from the host, which is the
            // intended fade-in.
            _fading = false;

            var isSameHost = _visible && context.Slot == _currentSlot;
            var style = _config.StyleFor(preview.Type);
            _dashSpacing = _config.DashSpacing;
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
                RefitPens(style);
            }
            else
            {
                AcquirePens(style, introduce);
            }

            _visible = true;
        }

        internal void Hide()
        {
            // Teardown-immediate: whatever a fade might have been doing is moot once every pen is about
            // to be returned to the pool outright.
            _fading = false;

            if (!_visible && _pens.Count == 0)
            {
                return;
            }

            ReleasePens();
            _visible = false;
        }

        /// <summary>
        ///     Releases nothing yet — stops every pen laying new ribbon and lets what's already drawn fade
        ///     on the <see cref="TrailRenderer" />'s own lifetime, then releases to the pool once that has
        ///     played out. Pens keep their positions while fading, so the figure holds its last shape
        ///     instead of collapsing to a point.
        /// </summary>
        internal void BeginHide()
        {
            // Idempotent: called every frame the controller has nothing to show, but only the first such
            // frame (still _visible) has anything to start — a call mid-fade, or after one has already
            // released, is a no-op rather than restarting the clock or double-releasing.
            if (_fading || !_visible)
            {
                return;
            }

            if (_pens.Count == 0)
            {
                _visible = false;
                return;
            }

            for (var i = 0; i < _pens.Count; i++)
            {
                var trail = _pens[i].Trail;
                if (trail != null)
                {
                    trail.SetEmitting(false);
                }
            }

            // Read off a live pen rather than re-authoring the duration here, so this can never drift
            // from what SetRibbonTime actually applied for this figure's style.
            _fadeDuration = _pens[0].Trail != null ? _pens[0].Trail.EffectiveRibbonSeconds : 0f;
            _fadeElapsed = 0f;
            _fading = true;
            _visible = false;
        }

        public void LateTick()
        {
            if (_fading)
            {
                AdvanceFade(Time.deltaTime);
                return;
            }

            if (!_visible)
            {
                return;
            }

            // Idempotent per frame, so it costs nothing extra when ItemRangePreviewController already
            // refreshed the same viewport earlier this frame.
            _viewport.Refresh();

            var deltaTime = Time.deltaTime;
            for (var i = 0; i < _pens.Count; i++)
            {
                var pen = _pens[i];
                AdvancePen(ref pen, deltaTime);
                _pens[i] = pen;
            }
        }

        // Pens hold their last position while fading (no AdvancePen calls here) — a fading ribbon should
        // hang where it stopped, not keep sweeping its dash while it dims.
        private void AdvanceFade(float deltaTime)
        {
            _fadeElapsed += deltaTime;
            if (_fadeElapsed < _fadeDuration)
            {
                return;
            }

            ReleasePens();
            _fading = false;
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
        //
        // Reads _strokeLengths, so callers must run this after BuildArcTable — on both the acquire and
        // the refit path, since a figure can reshape (Laser's hex-stepped rotation) while its host stays
        // the same, and _dashesPerStroke would otherwise go stale against the new geometry.
        private int DeriveDashCounts()
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

            return desiredTotal;
        }

        // Shared by Hide() and a fade's natural completion — the two moments every pen actually goes back
        // to the pool, as opposed to BeginHide, which only stops them emitting.
        private void ReleasePens()
        {
            for (var i = 0; i < _pens.Count; i++)
            {
                var trail = _pens[i].Trail;
                if (trail != null)
                {
                    _poolManager.Return(_poolKey, trail);
                }
            }

            _pens.Clear();
        }

        // Grows or shrinks the pen list to the wanted count, returning shed pens to the pool — shared by
        // the acquire and refit paths so both resize the same way; which pens end up "new" (Trail == null)
        // is left for the caller's dealing pass to notice and populate.
        private void ResizePens(int wanted)
        {
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
        }

        private void AcquirePens(IItemPreviewStyle style, bool introduce)
        {
            var strokeCount = _shape.Strokes.Count;
            var wanted = DeriveDashCounts();

            ResizePens(wanted);

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

                    if (introduce)
                    {
                        pen.BloomElapsed = 0f;
                        pen.Bloomed = false;
                    }
                    else
                    {
                        // A re-settle on a host already being telegraphed is not the figure's first
                        // appearance — hand the pen the same fully-settled state RefitPens gives a pen the
                        // figure only just grew into, so AdvancePen takes the settled branch on its very
                        // first tick and the pen simply appears at its place in the figure instead of
                        // spiralling back in from the origin.
                        pen.BloomElapsed = _config.BloomDuration + 1f;
                        pen.Bloomed = true;
                    }

                    // Spread over the whole pen set (global penIndex), not per-stroke (dashIndex) — pens
                    // are dealt stroke by stroke, so a per-stroke phase would clump the fan instead of
                    // spreading it evenly around the full circle.
                    pen.BloomPhaseDegrees = 360f * penIndex / _pens.Count;

                    pen.Trail.SetRibbonTime(style.RibbonSeconds);
                    pen.Trail.ClearRibbon();
                    pen.Trail.SetPosition(new Vector3(_origin.x, _origin.y, 0f));
                    pen.Trail.SetEmitting(_emitDuringBloom);
                    pen.Emitting = _emitDuringBloom;

                    _pens[penIndex] = pen;
                    penIndex++;
                }
            }
        }

        // Same host, re-fitted geometry: keep every surviving pen's bloom phase and progress, only
        // reassign the slot (StrokeIndex/DashIndex) it owns to match the new dash counts. A pen still
        // blooming needs no re-aim here — the warp re-derives from the live shape position every frame in
        // AdvancePen, so it just follows the refitted geometry.
        //
        // The dash counts themselves are NOT stable across a refit: BuildArcTable already ran against the
        // new shape, so DeriveDashCounts must run again here too, or AdvanceDash would divide the new
        // stroke lengths by a stale dash count left over from whatever geometry the host last had.
        private void RefitPens(IItemPreviewStyle style)
        {
            var strokeCount = _shape.Strokes.Count;
            var wanted = DeriveDashCounts();

            ResizePens(wanted);

            var penIndex = 0;
            for (var s = 0; s < strokeCount; s++)
            {
                var dashesOnStroke = _dashesPerStroke[s];
                for (var dashIndex = 0; dashIndex < dashesOnStroke; dashIndex++)
                {
                    var pen = _pens[penIndex];

                    // A pen carried over from before the resize (Trail already set) keeps its bloom and
                    // distance; only a slot the figure just grew into starts life with a null Trail.
                    var isNewPen = pen.Trail == null;
                    pen.Trail ??= _poolManager.GetOrRegister(
                        _poolKey, () => new SimplePoolChannel<HighlightTrail>(_penPrefab));
                    pen.StrokeIndex = s;
                    pen.DashIndex = dashIndex;

                    if (isNewPen)
                    {
                        // A pen the figure only just grew into is a different event from the figure first
                        // appearing — it should read as already part of the figure, not bloom in from the
                        // host, so it is handed the fully-settled state directly.
                        pen.Distance = 0f;
                        pen.Bloomed = true;
                        pen.BloomElapsed = _config.BloomDuration + 1f;
                        pen.Trail.SetRibbonTime(style.RibbonSeconds);
                    }

                    _pens[penIndex] = pen;
                    penIndex++;
                }
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

            Vector3 position;
            if (t >= 1f)
            {
                position = shapePos;

                // Fired once on the frame the warp first settles — Bloomed feeds the emit rule below,
                // which is what used to clear/re-enable here directly.
                pen.Bloomed = true;
            }
            else
            {
                var eased = _config.BloomCurve.Evaluate(t);
                var offsetX = shapePos.x - _origin.x;
                var offsetY = shapePos.y - _origin.y;
                var radians = (_config.BloomSweepDegrees + pen.BloomPhaseDegrees) * (1f - eased) * Mathf.Deg2Rad;
                var cos = Mathf.Cos(radians);
                var sin = Mathf.Sin(radians);
                var rotatedX = (offsetX * cos) - (offsetY * sin);
                var rotatedY = (offsetX * sin) + (offsetY * cos);

                position = new Vector3(
                    _origin.x + (rotatedX * eased),
                    _origin.y + (rotatedY * eased),
                    0f);
            }

            // A refit (same host, reshaped figure) can reassign this pen to a different stroke slot,
            // which jumps its position outright — the ribbon would otherwise draw a straight chord
            // across that jump instead of restarting at the new spot. Now a backstop rather than a case
            // that fires in normal play: ItemRangePreviewController only shows a figure once its inputs
            // have held still past the sight delay and never re-Shows while it stays visible, so a
            // visible figure's pens shouldn't reposition at all — RefitPens only ever runs on a Show the
            // controller itself no longer issues while shown. Left in for whatever reaches AdvancePen
            // outside that contract. Gated on Bloomed because during the bloom the warp is deliberately
            // carrying the pen far and fast from the host to its place in the figure, easily outrunning
            // this threshold every frame; checking there would clear the ribbon continuously and suppress
            // the launch entirely.
            if (pen.Bloomed && pen.HasLastPosition)
            {
                var teleportThreshold = Mathf.Max(
                    _config.TraceSpeed * deltaTime * TeleportSpeedMultiplier, MinTeleportDistance);
                if ((position - pen.LastPosition).sqrMagnitude > teleportThreshold * teleportThreshold)
                {
                    pen.Trail.ClearRibbon();
                }
            }

            pen.LastPosition = position;
            pen.HasLastPosition = true;

            pen.Trail.SetPosition(position);

            var visible = !_viewport.IsActive || _viewport.Contains(position);
            var wantEmit = visible && (pen.Bloomed || _emitDuringBloom);
            if (wantEmit != pen.Emitting)
            {
                // Re-entry clears first: the ribbon still holds the points from before the pen left, and
                // re-enabling without clearing draws a straight chord from where it exited to where it came
                // back in -- the jump this cull exists to avoid.
                if (wantEmit)
                {
                    pen.Trail.ClearRibbon();
                }

                pen.Trail.SetEmitting(wantEmit);
                pen.Emitting = wantEmit;
            }
        }

        // One pen draws ONE dash, and the dashed line is the pens sitting next to each other — ask for
        // three dashes and you get three pens, each owning a third of the stroke. Dashing is the only
        // drawing style now: zero spacing (DashSpacing == 0) means painted == slotLength below and
        // adjacent dashes touch with no gap — a solid line falls out of this same code rather than
        // needing a separate continuous-mode branch.
        //
        // Within its own slot a pen loops: it paints for the derived dash length, lifts for the pinned
        // gap, and wraps back to its slot start to redraw. Pen up/down via emitting, never ClearRibbon —
        // clearing wipes what was already painted, which is what made an earlier attempt read as one
        // short stroke sliding along the figure instead of a dashed line.
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
            // dashes is simply arc no pen ever visits (the gap below).
            //
            // The earlier a → b, snap-back-to-a, repeat is what strobed: every snap ended one ribbon and
            // began another, so the ribbon lifetime decided how many stale copies piled up behind it.
            //
            // Rounding the dash count per stroke (AcquirePens) means slotLength is never exactly the
            // authored stride, so one of {dash, gap} must absorb that per-stroke error. The gap is what
            // the eye reads as rhythm, so it is pinned at exactly _dashSpacing and the dash absorbs the
            // error instead — that is what makes two strokes of very different length (Laser's two
            // corridors) read as the same dash pattern. Floored at a fraction of the slot rather than
            // let the subtraction go non-positive, so a very short stroke (or a spacing authored larger
            // than the stride) still draws something instead of a vanished dash.
            var painted = Mathf.Max(slotLength * 0.1f, slotLength - _dashSpacing);
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

            // Mirrors the trail's own emitting flag, so AdvancePen only calls into the renderer on a real
            // edge (bloom settling, or crossing the visible-rect boundary) instead of every frame.
            public bool Emitting;

            // The slot this pen owns for its whole life. One pen draws one dash — the dashed line is the
            // pens sitting side by side, not one pen visiting every slot.
            public int DashIndex;

            // Evenly spaced launch bearings so the set fans out radially on bloom instead of turning as
            // one rigid stick; decays away with the shared sweep, so it never moves the landing position.
            public float BloomPhaseDegrees;

            // Last frame's position, for teleport detection in AdvancePen. HasLastPosition guards a
            // freshly acquired pen (default Vector3.zero) from reading as having teleported from the
            // origin on its very first tick.
            public Vector3 LastPosition;
            public bool HasLastPosition;
        }
    }
}
