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

        // A retained copy of context.TracePoints, plus its own cumulative-arc table (mirroring _arcTable,
        // but for the trace polyline rather than a shape stroke — always open, since a trace never
        // closes). Copied rather than aliased: the provider's buffer is rewritten in place every Tick, and
        // sampling it mid-rewrite would show a torn frame. Rebuilt per Show/refit, read every frame by
        // AdvanceCascade so the leading pen's approach can follow a bent trace instead of cutting across it.
        // _traceLength and _hostTraceOffset (below, with the other mutable fields) are derived alongside it.
        private readonly List<Vector3> _tracePoints = new();
        private readonly List<float> _traceArcTable = new();

        // Where each stroke's own entry point sits, as an arc-length offset from the stroke's own start —
        // one entry per stroke, parallel to _strokeLengths. No longer a travel destination for every pen;
        // it only ranks the cascade order (ComputeTravelDistance) and anchors the leading pen's approach.
        private readonly List<float> _entryOffsets = new();

        // The entry point itself, in world space, alongside the arc-length distance the leading pen's
        // approach actually covers — along the trace when _entryTraceValid says the trace can answer,
        // otherwise the straight-line fallback distance to _entryPoints. Only the stroke's LEADING pen
        // (CascadeRank 0) ever travels this leg now — every other pen on the stroke never leaves its own
        // dash slot, which is the whole point of the cascade (see AdvanceCascade).
        private readonly List<Vector3> _entryPoints = new();
        private readonly List<float> _approachLengths = new();

        // The entry point's own arc-length offset along the trace, parallel to _entryOffsets, and whether
        // that offset is trustworthy — false when the trace itself is unusable (fewer than two points) or
        // this stroke's entry helper never found a crossing (Lightning's later arcs, reached only through
        // the chain), in which case the approach falls back to a straight line to _entryPoints instead.
        private readonly List<float> _entryTraceOffsets = new();
        private readonly List<bool> _entryTraceValid = new();

        // Dashes derived per stroke this Show pass — sized to stroke count and reused rather than
        // reallocated, so re-aiming at a new host allocates nothing. Doubles as the pens-per-stroke count,
        // since dashing is universal now: one pen per dash, by definition.
        private readonly List<int> _dashesPerStroke = new();

        // Per-stroke cascade timing, derived once per Show from BloomDuration (see BuildCascadeTimings):
        // the leading pen's approach gets a share proportional to its length against the stroke's total
        // path, and the remainder splits evenly across that stroke's dash count, so pen k's window starts
        // exactly when pen k − 1's ends. Every stroke reads the very same BloomElapsed clock — only these
        // per-stroke window bounds differ — which is what makes several strokes (Laser's two lines,
        // Lightning's arcs) cascade in parallel rather than one after another.
        private readonly List<float> _approachDurations = new();
        private readonly List<float> _dashDurations = new();

        // Scratch space for AssignCascadeRanks — one ComputeTravelDistance result per dash on the stroke
        // currently being ranked. Cleared and refilled per stroke rather than allocated, since ranking
        // runs once per Show, not per frame.
        private readonly List<float> _cascadeScratch = new();

        private Vector2 _origin;
        private Vector2Int _currentSlot;
        private bool _visible;

        // Total arc length of _tracePoints, and the host's own position along it as an arc-length offset
        // from the trace's first point — both derived by BuildTraceBuffers alongside _traceArcTable above.
        // The trace runs from the projectile through the board and crosses the host somewhere along the
        // way, so the host offset is generally non-zero; together these anchor the leading pen's approach.
        private float _traceLength;
        private float _hostTraceOffset;

        // A graceful hide stops emitting but keeps the pens where they are, so what was already drawn
        // fades out over the ribbon's own lifetime instead of vanishing. _fadeDuration is that lifetime,
        // read off a pen's trail at BeginHide rather than authored again here (see
        // HighlightTrail.EffectiveRibbonSeconds); _fadeElapsed tracks how far into it LateTick is.
        private bool _fading;
        private float _fadeElapsed;
        private float _fadeDuration;

        // Captured on Show so LateTick doesn't re-resolve the config every frame.
        private float _dashSpacing;

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
        ///     True for a figure's first appearance on this host — pens cascade in one dash at a time: each
        ///     stroke's leading pen approaches, emitting the whole way, from the host origin to the
        ///     stroke's entry point, then draws its own dash; every other pen on the stroke waits, parked
        ///     pen-up, at its own dash's start until its turn, then draws that dash and hands off to the
        ///     next. False for a re-settle
        ///     on a host already being telegraphed (the aim nudged but landed on the same host): pens
        ///     acquired here start already in their settled, post-bloom state, so the figure reappears in
        ///     place instead of cascading in again. <see cref="ItemRangePreviewController" /> decides which
        ///     this is by tracking the slot it last actually showed.
        /// </param>
        /// <remarks>
        ///     Called on every aim change while a host stays sighted, so it distinguishes the two cases by
        ///     <see cref="ItemPreviewContext.Slot" />: a DIFFERENT host restarts the pens (they bloom in
        ///     again from their strokes' entry points), while the SAME host only re-fits the geometry — the
        ///     figure follows a drifting balloon, or a Shield stub follows the aim tip, without every pen
        ///     restarting its bloom each time the player nudges the aim. In current play the controller
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
            _dashSpacing = _config.DashSpacing;
            _origin = context.Origin;
            _currentSlot = context.Slot;
            BuildArcTable();
            BuildTraceBuffers(context.TracePoints);

            // Reads _arcTable and the trace buffers built just above, so must run after both — the offset
            // an entry point resolves to is an arc length along the stroke, and the trace offset alongside
            // it is an arc length along the (possibly bent) trace, not a raw geometry comparison.
            BuildEntryOffsets();

            if (isSameHost)
            {
                RefitPens();
            }
            else
            {
                AcquirePens(introduce);
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
            // from the pen prefab's own authored ribbon lifetime.
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

        // Copies the live trace into a reusable buffer and builds its own cumulative-arc table, mirroring
        // BuildArcTable but for the trace polyline rather than one of the shape's strokes — done once per
        // Show/refit so AdvanceCascade can sample along it every frame without re-copying or re-walking
        // it. Also resolves the host's own position on the trace (_hostTraceOffset), since it depends on
        // nothing but the buffer just built and _origin, already set by the time this runs.
        private void BuildTraceBuffers(IReadOnlyList<Vector3> tracePoints)
        {
            _tracePoints.Clear();
            _traceArcTable.Clear();
            _traceLength = 0f;
            _hostTraceOffset = 0f;

            if (tracePoints == null || tracePoints.Count == 0)
            {
                return;
            }

            for (var i = 0; i < tracePoints.Count; i++)
            {
                _tracePoints.Add(tracePoints[i]);
            }

            _traceArcTable.Add(0f);
            var total = 0f;
            for (var i = 1; i < _tracePoints.Count; i++)
            {
                total += Vector3.Distance(_tracePoints[i - 1], _tracePoints[i]);
                _traceArcTable.Add(total);
            }

            _traceLength = total;
            _hostTraceOffset = NearestOffsetOnPolyline(
                _tracePoints, _traceArcTable, new Vector3(_origin.x, _origin.y, 0f));
        }

        // Where the closest point of an arbitrary polyline to a target sits, as an arc-length offset from
        // the polyline's own first point — an O(n) walk over every segment, fine here since it runs once
        // per Show/refit (for the host's own position on the trace), never per frame or per pen.
        private static float NearestOffsetOnPolyline(
            IReadOnlyList<Vector3> points, IReadOnlyList<float> arcTable, Vector3 target)
        {
            var bestOffset = 0f;
            var bestDistanceSqr = float.MaxValue;

            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var segment = points[i + 1] - a;
                var segmentLengthSqr = segment.sqrMagnitude;

                var t = segmentLengthSqr <= 1e-10f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(target - a, segment) / segmentLengthSqr);

                var candidate = a + (segment * t);
                var distanceSqr = (target - candidate).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                bestOffset = arcTable[i] + (t * Mathf.Sqrt(segmentLengthSqr));
            }

            return bestOffset;
        }

        // Where each stroke's own figure-drawing starts: the arc-length offset of the point where the
        // aim's line of sight first crosses it, via the pure geometry helper. Falls back to the stroke's
        // own start (0) when the trace never crosses it at all — true of Lightning's later arcs, which the
        // trace only reaches through the chain, not directly; starting those at 0 makes each arc draw in
        // sequence after the one before it, which is the wanted look anyway rather than a special case.
        //
        // Also resolves that offset to a world point, and the arc-length distance the leading pen's
        // approach actually covers: along the trace, between _hostTraceOffset and this stroke's own entry
        // trace offset, when the helper found a crossing; otherwise the straight-line distance to the
        // entry point, same as before the trace-following approach existed. Reads _tracePoints and
        // _traceArcTable, so must run after BuildTraceBuffers.
        private void BuildEntryOffsets()
        {
            _entryOffsets.Clear();
            _entryPoints.Clear();
            _approachLengths.Clear();
            _entryTraceOffsets.Clear();
            _entryTraceValid.Clear();

            var origin = new Vector3(_origin.x, _origin.y, 0f);
            for (var s = 0; s < _shape.Strokes.Count; s++)
            {
                var found = ItemPreviewEntry.TryFindEntryOffset(
                    _shape, s, _arcTable, _tracePoints, out var offset, out var traceOffset);
                offset = found ? offset : 0f;
                _entryOffsets.Add(offset);

                var entryPoint = SampleStroke(s, offset);
                _entryPoints.Add(entryPoint);

                _entryTraceValid.Add(found);
                _entryTraceOffsets.Add(found ? traceOffset : 0f);

                var approachLength = found
                    ? Mathf.Abs(traceOffset - _hostTraceOffset)
                    : Vector3.Distance(origin, entryPoint);
                _approachLengths.Add(approachLength);
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

        // The arc length a pen actually paints within its slot, factored out so BuildCascadeTimings' split
        // and AdvanceDash's own ping-pong always agree on the identical value — see AdvanceDash for why the
        // gap, not the dash, is the pinned quantity.
        private float ComputePaintedLength(float slotLength)
        {
            return Mathf.Max(slotLength * 0.1f, slotLength - _dashSpacing);
        }

        // Splits BloomDuration into this stroke's own cascade windows: the leading pen's approach gets a
        // share proportional to its length against the stroke's total path (approach length + every dash's
        // own painted length), and the remainder divides evenly across the stroke's dash count, so pen k's
        // window starts exactly when pen k − 1's ends (AdvanceCascade). Reads _dashesPerStroke and
        // _approachLengths, so callers must run this after DeriveDashCounts and BuildEntryOffsets, on both
        // the acquire and the refit path — a re-fit can change either a stroke's dash count or its entry
        // offset, and the windows would otherwise go stale against the new geometry.
        private void BuildCascadeTimings()
        {
            _approachDurations.Clear();
            _dashDurations.Clear();

            var bloomDuration = _config.BloomDuration;
            for (var s = 0; s < _shape.Strokes.Count; s++)
            {
                var dashesOnStroke = _dashesPerStroke[s];
                var slotLength = _strokeLengths[s] / dashesOnStroke;
                var painted = ComputePaintedLength(slotLength);
                var approachLength = _approachLengths[s];
                var total = approachLength + (painted * dashesOnStroke);

                // A near-zero approach (the entry sits on the host itself, e.g. Paint's apex or
                // Lightning's first arc) is treated as already arrived rather than divided by — the
                // leading pen gets no approach window at all, and starts drawing its own dash at t = 0.
                var approachDuration = approachLength > 1e-4f && total > 1e-4f
                    ? bloomDuration * (approachLength / total)
                    : 0f;

                _approachDurations.Add(approachDuration);
                _dashDurations.Add((bloomDuration - approachDuration) / dashesOnStroke);
            }
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

        private void AcquirePens(bool introduce)
        {
            var strokeCount = _shape.Strokes.Count;
            var wanted = DeriveDashCounts();
            BuildCascadeTimings();

            ResizePens(wanted);

            var penIndex = 0;
            for (var s = 0; s < strokeCount; s++)
            {
                var dashesOnStroke = _dashesPerStroke[s];
                var slotLength = _strokeLengths[s] / dashesOnStroke;
                var strokeStartIndex = penIndex;

                for (var dashIndex = 0; dashIndex < dashesOnStroke; dashIndex++)
                {
                    var pen = _pens[penIndex];
                    pen.Trail ??= _poolManager.GetOrRegister(
                        _poolKey, () => new SimplePoolChannel<HighlightTrail>(_penPrefab));
                    pen.StrokeIndex = s;
                    pen.DashIndex = dashIndex;

                    // Distance only matters once Bloomed (AdvanceDash's own ping-pong) — the cascade
                    // itself is driven by the shared clock in AdvanceCascade, not by accumulated distance,
                    // so this just primes the value AdvanceDash will inherit at handoff.
                    pen.Distance = 0f;

                    if (introduce)
                    {
                        pen.BloomElapsed = 0f;
                        pen.Bloomed = false;
                        pen.Parked = true;
                    }
                    else
                    {
                        // A re-settle on a host already being telegraphed is not the figure's first
                        // appearance — hand the pen the same fully-settled state RefitPens gives a pen the
                        // figure only just grew into, so AdvancePen takes the settled branch on its very
                        // first tick and the pen simply appears at its place in the figure instead of
                        // cascading in again. Bloomed = true skips AdvanceCascade forever after, so Parked
                        // must be cleared explicitly here — a reused pen's could otherwise still read true
                        // from whatever figure it last belonged to and leave it dark.
                        pen.BloomElapsed = _config.BloomDuration + 1f;
                        pen.Bloomed = true;
                        pen.Parked = false;
                    }

                    // Parked pen-up at the origin rather than seeded emitting: AdvancePen's rising edge
                    // (visible flips true) is what starts the ribbon, and that edge clears first and runs
                    // after SetPosition, so the ribbon opens clean at the pen's real first position —
                    // wherever the leading pen's approach or a waiting pen's own dash start puts it — never
                    // a chord from this parked origin to there.
                    pen.Trail.ClearRibbon();
                    pen.Trail.SetPosition(new Vector3(_origin.x, _origin.y, 0f));
                    pen.Trail.SetEmitting(false);
                    pen.Emitting = false;

                    // A reused pen's teleport baseline is stale from its previous figure, and a freshly
                    // grown one already reads false — either way it must not be trusted, so clear it
                    // explicitly rather than rely on carryover.
                    pen.HasLastPosition = false;
                    pen.LastPosition = default;

                    _pens[penIndex] = pen;
                    penIndex++;
                }

                AssignCascadeRanks(s, strokeStartIndex, dashesOnStroke, slotLength);
            }
        }

        // Same host, re-fitted geometry: keep every surviving pen's bloom progress, only reassign the slot
        // (StrokeIndex/DashIndex) it owns and, with it, its cascade rank and window, to match the new dash
        // counts. A pen still cascading needs no other re-aim here — its position re-derives from the live
        // entry offset, rank and window every frame in AdvanceCascade, so it just follows the refitted
        // geometry.
        //
        // The dash counts themselves are NOT stable across a refit: BuildArcTable already ran against the
        // new shape, so DeriveDashCounts must run again here too, or AdvanceDash would divide the new
        // stroke lengths by a stale dash count left over from whatever geometry the host last had.
        private void RefitPens()
        {
            var strokeCount = _shape.Strokes.Count;
            var wanted = DeriveDashCounts();
            BuildCascadeTimings();

            ResizePens(wanted);

            var penIndex = 0;
            for (var s = 0; s < strokeCount; s++)
            {
                var dashesOnStroke = _dashesPerStroke[s];
                var slotLength = _strokeLengths[s] / dashesOnStroke;
                var strokeStartIndex = penIndex;

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
                        // appearing — it should read as already part of the figure, not cascade in from its
                        // own dash start, so it is handed the fully-settled state directly.
                        pen.Distance = 0f;
                        pen.Bloomed = true;
                        pen.BloomElapsed = _config.BloomDuration + 1f;
                        pen.Parked = false;
                    }

                    _pens[penIndex] = pen;
                    penIndex++;
                }

                AssignCascadeRanks(s, strokeStartIndex, dashesOnStroke, slotLength);
            }
        }

        // Forward arc distance from this stroke's entry to this dash's own slot start — used only to RANK
        // the stroke's pens into cascade order now (AssignCascadeRanks); the pen no longer travels this
        // distance. A closed stroke always measures forward (Repeat wraps a slot-before-entry distance the
        // long way round instead of going negative), so ranking by it puts every pen in the order the
        // cascade sweeps around the shape. An open stroke has no wrap to enforce that with, so the plain
        // signed difference is used instead — it still ranks correctly, since a slot's raw position along
        // the stroke and its distance from the (fixed) entry differ only by a constant offset.
        private float ComputeTravelDistance(int strokeIndex, int dashIndex, float slotLength)
        {
            var slotOffset = dashIndex * slotLength;
            var entryOffset = _entryOffsets[strokeIndex];

            return _shape.Strokes[strokeIndex].Closed
                ? Mathf.Repeat(slotOffset - entryOffset, _strokeLengths[strokeIndex])
                : slotOffset - entryOffset;
        }

        // Ranks this stroke's just-assigned pens by forward distance from the entry (smallest first) into
        // CascadeRank, so AdvanceCascade knows each pen's turn in the relay — rank 0 is the leading pen,
        // the only one that ever approaches from the host. A plain O(n²) compare is fine here: it runs
        // once per Show/refit, never per frame, and n is bounded by MaxPens.
        private void AssignCascadeRanks(int strokeIndex, int startPenIndex, int dashesOnStroke, float slotLength)
        {
            _cascadeScratch.Clear();
            for (var dashIndex = 0; dashIndex < dashesOnStroke; dashIndex++)
            {
                _cascadeScratch.Add(ComputeTravelDistance(strokeIndex, dashIndex, slotLength));
            }

            for (var i = 0; i < dashesOnStroke; i++)
            {
                var travel = _cascadeScratch[i];
                var rank = 0;
                for (var j = 0; j < dashesOnStroke; j++)
                {
                    // Ties (only possible at slotLength == 0, a degenerate stroke) fall back to dash
                    // index order, so rank is always a total order with no two pens sharing a slot.
                    if (j == i)
                    {
                        continue;
                    }

                    var otherTravel = _cascadeScratch[j];
                    if (otherTravel < travel || (otherTravel == travel && j < i))
                    {
                        rank++;
                    }
                }

                var pen = _pens[startPenIndex + i];
                pen.CascadeRank = rank;
                _pens[startPenIndex + i] = pen;
            }
        }

        // Dispatches to whichever phase this pen is in — cascading in (waiting its turn, the leading pen's
        // approach, or sweeping its own dash for the first time), or (once Bloomed) settled into its dash
        // slot's own ping-pong — then applies the result uniformly (teleport guard, positioning,
        // visibility-driven emit) regardless of which phase produced it.
        private void AdvancePen(ref Pen pen, float deltaTime)
        {
            if (pen.Trail == null)
            {
                return;
            }

            var position = pen.Bloomed ? AdvanceDash(ref pen, deltaTime) : AdvanceCascade(ref pen, deltaTime);
            ApplyPenPosition(ref pen, position, deltaTime);
        }

        // One shared progress clock (t, off BloomDuration, eased by BloomCurve) drives every pen on every
        // stroke — NOT a per-pen ease over that pen's own window. Under one shared clock, pen k's window
        // opens exactly when pen k − 1's closes (see BuildCascadeTimings), so dashes light up one after
        // another in cascade-rank order: this is what reads as the shape drawing itself in one dash at a
        // time, instead of every pen tracing the same shared path at once — the stacking this cascade
        // exists to replace. Multiple strokes cascade in parallel for the same reason: every stroke reads
        // this identical elapsed/eased value, just against its own _approachDurations/_dashDurations, so a
        // two-stroke figure (Laser's two lines, Lightning's arcs) draws both at once rather than in series.
        //
        // Only CascadeRank 0 — the leading pen — ever gets an approach: an emitting sweep along the trace
        // from the host origin to the stroke's entry point. It draws because the leg is a continuous walk
        // along the trace polyline — there is no discontinuity on it to hide, unlike a parked pen's jump
        // into its own dash — so ink flows out of the balloon and along the aim before the dash itself
        // starts forming at the entry point. Every other pen simply waits, Parked and pen-up, at its own
        // dash's start position until its window opens; nothing else ever leaves the host, so nothing else
        // can stack a ribbon onto the shared approach path.
        private Vector3 AdvanceCascade(ref Pen pen, float deltaTime)
        {
            pen.BloomElapsed += deltaTime;

            var duration = Mathf.Max(_config.BloomDuration, 1e-4f);
            var t = Mathf.Clamp01(pen.BloomElapsed / duration);
            var elapsed = _config.BloomCurve.Evaluate(t) * _config.BloomDuration;

            var strokeIndex = pen.StrokeIndex;
            var approachDuration = _approachDurations[strokeIndex];

            if (pen.CascadeRank == 0 && elapsed < approachDuration)
            {
                // Not Parked: the approach must draw, so ApplyPenPosition's !pen.Parked visibility term
                // has to read true for the whole leg, not just once the dash itself starts.
                pen.Parked = false;
                var approachT = approachDuration > 1e-4f ? elapsed / approachDuration : 1f;
                return SampleApproach(strokeIndex, approachT);
            }

            // The leading pen falls through here the instant its approach window closes (windowStart for
            // rank 0 is exactly approachDuration), so it never has a separate waiting frame between
            // approaching and drawing — the two legs hand off on the very same tick, and the ribbon is
            // never cleared at the seam since the position is continuous across it.
            var dashDuration = _dashDurations[strokeIndex];
            var windowStart = approachDuration + (pen.CascadeRank * dashDuration);

            var slotLength = _strokeLengths[strokeIndex] / _dashesPerStroke[strokeIndex];
            var dashStart = pen.DashIndex * slotLength;

            if (elapsed < windowStart)
            {
                pen.Parked = true;
                return SampleStroke(strokeIndex, dashStart);
            }

            pen.Parked = false;
            var painted = ComputePaintedLength(slotLength);
            var localT = dashDuration > 1e-4f ? Mathf.Clamp01((elapsed - windowStart) / dashDuration) : 1f;

            // localT == 1 is the normal handoff signal, but it depends on `elapsed` (the EASED clock)
            // landing exactly on this window's own end. For every pen but the last, that end is strictly
            // interior to the stroke's total window and elapsed sails past it well before t reaches 1, so
            // drift is invisible. The LAST pen's window end is arithmetically equal to BloomDuration
            // itself (BuildCascadeTimings), so its promotion depends on the eased value landing exactly on
            // the raw clock's own ceiling — and BuildCascadeTimings' division doesn't reliably round-trip
            // to that ceiling in float, so elapsed lands a hair short and localT stalls just below 1
            // forever, stranding the pen mid-dash with a frozen, still-emitting ribbon that ages out. The
            // raw, clamped `t` reaches exactly 1 by construction regardless of that arithmetic, so it is
            // used as a second, unconditional promotion signal — one that also covers an authored
            // BloomCurve that never reaches 1 at t == 1, which would otherwise strand every pen whose
            // window closes late, not just the last one.
            if (localT >= 1f || t >= 1f)
            {
                // Force localT to the ping-pong's own peak so the position below lands exactly where
                // AdvanceDash's next tick continues the sweep from — no separate jump between the frozen
                // spot and the handoff point.
                localT = 1f;

                // Fired once on the frame this pen promotes — Bloomed both stops AdvanceCascade running
                // next frame (AdvancePen dispatches to AdvanceDash instead) and feeds the emit/teleport
                // rules in ApplyPenPosition. Distance is pinned to painted — the ping-pong's own peak — so
                // AdvanceDash's next tick continues the same sweep outward instead of restarting it; the
                // two formulas agree exactly at this handoff.
                pen.Bloomed = true;
                pen.Distance = painted;
            }

            return SampleStroke(strokeIndex, dashStart + (localT * painted));
        }

        // Teleport guard, positioning, and the visibility-driven emit edge — split out of AdvancePen so the
        // per-frame per-pen phase dispatch stays a single branch rather than growing past the audit's
        // complexity ceiling.
        private void ApplyPenPosition(ref Pen pen, Vector3 position, float deltaTime)
        {
            // A refit (same host, reshaped figure) can reassign this pen to a different stroke slot, which
            // jumps its position outright — the ribbon would otherwise draw a straight chord across that
            // jump instead of restarting at the new spot. Now a backstop rather than a case that fires in
            // normal play: ItemRangePreviewController only shows a figure once its inputs have held still
            // past the sight delay and never re-Shows while it stays visible, so a visible figure's pens
            // shouldn't reposition at all — RefitPens only ever runs on a Show the controller itself no
            // longer issues while shown. Left in for whatever reaches AdvancePen outside that contract.
            // Gated on Bloomed because mid-cascade the pen can deliberately move far and fast — the
            // leading pen's approach, or a waiting pen's jump from its parked spot into its dash the
            // instant its window opens — easily outrunning this threshold every frame; checking there
            // would clear the ribbon continuously and suppress the draw-in entirely.
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

            // A pen draws once its own cascade window opens — the sweep within that window IS what draws
            // its dash in — and the leading pen also draws through its approach, since that leg is a
            // continuous walk with nothing to hide. A pen stays dark only while Parked: a later pen sitting
            // at its own dash start until its turn, reached by a discontinuous jump it must not reveal.
            // Visibility decides the edge the rest of the time.
            var visible = !pen.Parked && (!_viewport.IsActive || _viewport.Contains(position));
            if (visible != pen.Emitting)
            {
                // Re-entry clears first: the ribbon still holds the points from before the pen left, and
                // re-enabling without clearing draws a straight chord from where it exited to where it came
                // back in -- the jump this cull exists to avoid.
                if (visible)
                {
                    pen.Trail.ClearRibbon();
                }

                pen.Trail.SetEmitting(visible);
                pen.Emitting = visible;
            }
        }

        // The leading pen's approach: sweeps along the trace polyline between the host's own position on
        // it (_hostTraceOffset) and this stroke's entry point (_entryTraceOffsets), so a bent aim line —
        // a wall bounce, a deflection — is followed rather than cut across in a straight line. Shield is
        // the figure this matters most for, since its entry sits at the far end of the aim line, but it
        // is a general fix: any figure whose entry isn't adjacent to the host bends with the trace now.
        // The two offsets can fall in either order (the entry can sit behind the host along the trace) —
        // Lerp moves continuously from one to the other regardless of which is larger, so both directions
        // just work. Falls back to the straight host-to-entry lerp used before this existed when the
        // trace can't answer for this stroke (BuildEntryOffsets already decided that per stroke).
        private Vector3 SampleApproach(int strokeIndex, float t)
        {
            if (_entryTraceValid[strokeIndex])
            {
                var distance = Mathf.Lerp(_hostTraceOffset, _entryTraceOffsets[strokeIndex], t);
                return SampleTrace(distance);
            }

            var origin = new Vector3(_origin.x, _origin.y, 0f);
            return Vector3.Lerp(origin, _entryPoints[strokeIndex], t);
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
            var painted = ComputePaintedLength(slotLength);
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

        // Mirrors SampleStroke for the trace polyline instead of a stroke — but a trace never wraps or
        // ping-pongs (it isn't a figure being drawn, just a path the approach samples a single leg of), so
        // out-of-range distances simply clamp to its ends rather than repeating or bouncing.
        private Vector3 SampleTrace(float distance)
        {
            if (_traceLength <= 1e-5f)
            {
                return _tracePoints.Count > 0 ? _tracePoints[0] : Vector3.zero;
            }

            distance = Mathf.Clamp(distance, 0f, _traceLength);

            var lastIndex = _tracePoints.Count - 1;
            for (var i = 1; i <= lastIndex; i++)
            {
                if (distance > _traceArcTable[i])
                {
                    continue;
                }

                var segmentLength = _traceArcTable[i] - _traceArcTable[i - 1];
                var segmentT = segmentLength <= 1e-5f ? 0f : (distance - _traceArcTable[i - 1]) / segmentLength;
                return Vector3.Lerp(_tracePoints[i - 1], _tracePoints[i], segmentT);
            }

            return _tracePoints[lastIndex];
        }

        private struct Pen
        {
            public HighlightTrail Trail;
            public int StrokeIndex;
            public float BloomElapsed;

            // Arc length travelled inside this pen's OWN slot, wrapped by the slot length. Untouched by
            // AdvanceCascade until the frame it hands off to AdvanceDash (pinned to the painted length
            // then), so the ping-pong sweep continues outward rather than restarting.
            public float Distance;
            public bool Bloomed;

            // True only while this pen is parked, pen-up, at its own dash start awaiting its cascade
            // window — it reaches that spot by a discontinuous jump, so it must stay dark until its turn.
            // NOT true during the leading pen's approach (host origin to the stroke's entry point): that
            // leg is a continuous walk along the trace with no discontinuity to hide, so it draws instead.
            // Set each tick by AdvanceCascade and read by ApplyPenPosition to decide the dark/lit edge —
            // recorded on the pen rather than recomputed in both places.
            public bool Parked;

            // Mirrors the trail's own emitting flag, so AdvancePen only calls into the renderer on a real
            // edge (bloom settling, or crossing the visible-rect boundary) instead of every frame.
            public bool Emitting;

            // The slot this pen owns for its whole life. One pen draws one dash — the dashed line is the
            // pens sitting side by side, not one pen visiting every slot.
            public int DashIndex;

            // This pen's position in its stroke's relay order — 0 is the leading pen (the only one that
            // approaches from the host), 1 draws next once rank 0's window closes, and so on. Computed
            // once per Show/refit by AssignCascadeRanks from ComputeTravelDistance's forward-arc ordering,
            // not per frame; AdvanceCascade turns it into a time window via _approachDurations/
            // _dashDurations.
            public int CascadeRank;

            // Last frame's position, for teleport detection in AdvancePen. HasLastPosition guards a
            // freshly acquired pen (default Vector3.zero) from reading as having teleported from the
            // origin on its very first tick.
            public Vector3 LastPosition;
            public bool HasLastPosition;
        }
    }
}
