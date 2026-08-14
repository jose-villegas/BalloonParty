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
    ///     rather than a registry of concurrent previews. Arbitration of WHICH host is sighted, and WHAT
    ///     draws next once a loop cycle completes, both belong to <see cref="ItemRangePreviewController" />
    ///     — this ticker only owns HOW a figure draws and times its own cycle (see <see cref="CycleComplete" />).
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

        // One pen per approach dash — the comet's tail. A single pen can't produce this: toggling
        // TrailRenderer.emitting off and on again bridges every gap with a straight chord (it keeps its
        // recorded points across the gap), and clearing on re-enable avoids the chord but erases the dash
        // just drawn. So each dash of the approach gets its own pen, cascading host-to-entry on the same
        // clock the figure's own dashes wait through, and each RETIRES — goes pen-up for good — the
        // instant its own dash is drawn, rather than settling into AdvanceDash's ping-pong (see
        // AdvanceApproachPen). Sized per Show/refit by DeriveApproachDashCounts, parallel to _pens but a
        // separate list since these pens never join the figure's own per-stroke dash cascade.
        private readonly List<ApproachPen> _approachPens = new();

        // Cumulative arc length per stroke, flattened: _arcTable[stroke.Start + i] is the distance from the
        // stroke's first point to its i-th. Rebuilt per Show, sized to the shape's own point count.
        private readonly List<float> _arcTable = new();
        private readonly List<float> _strokeLengths = new();

        // A retained copy of context.TracePoints, plus its own cumulative-arc table (mirroring _arcTable,
        // but for the trace polyline rather than a shape stroke — always open, since a trace never
        // closes). Copied rather than aliased: the provider's buffer is rewritten in place every Tick, and
        // sampling it mid-rewrite would show a torn frame. Rebuilt per Show/refit, read every frame by
        // AdvanceApproachPen so the approach cascade can follow a bent trace instead of cutting across it.
        // _traceLength and _hostTraceOffset (below, with the other mutable fields) are derived alongside it.
        private readonly List<Vector3> _tracePoints = new();
        private readonly List<float> _traceArcTable = new();

        // Where each stroke's own entry point sits, as an arc-length offset from the stroke's own start —
        // one entry per stroke, parallel to _strokeLengths. No longer a travel destination for every figure
        // pen; it only ranks the cascade order (ComputeTravelDistance) and anchors the stroke's own
        // approach cascade.
        private readonly List<float> _entryOffsets = new();

        // The entry point itself, in world space, alongside the arc-length distance the approach cascade
        // actually covers — along the trace when _entryTraceValid says the trace can answer, otherwise the
        // straight-line fallback distance to _entryPoints. This is now the shared path the stroke's
        // dedicated approach pens divide into their own dashes (DeriveApproachDashCounts); no figure pen
        // ever leaves its own dash slot — that is still the whole point of the cascade (see
        // AdvanceCascade).
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

        // Approach dashes derived per stroke, parallel to _dashesPerStroke but for the leading leg rather
        // than the figure itself — see DeriveApproachDashCounts for how the two share MaxPens with the
        // figure's own dashes always winning. Zero for a stroke whose entry sits on the host already
        // (Laser, Paint, Lightning's first arc): no approach, no approach pens.
        private readonly List<int> _approachDashesPerStroke = new();

        // Per-stroke cascade timing, derived once per Show from BloomDuration (see BuildCascadeTimings):
        // the stroke's own approach cascade gets a share proportional to its length against the stroke's
        // total path, and the remainder splits evenly across that stroke's figure dash count, so figure
        // pen k's window starts exactly when pen k − 1's ends. Every stroke reads the very same BloomElapsed
        // clock — only these per-stroke window bounds differ — which is what makes several strokes (Laser's
        // two lines, Lightning's arcs) cascade in parallel rather than one after another.
        private readonly List<float> _approachDurations = new();
        private readonly List<float> _dashDurations = new();

        // Scratch space for AssignCascadeRanks — one ComputeTravelDistance result per dash on the stroke
        // currently being ranked. Cleared and refilled per stroke rather than allocated, since ranking
        // runs once per Show, not per frame.
        private readonly List<float> _cascadeScratch = new();

        private Vector2 _origin;
        private Vector2Int _currentSlot;
        private bool _visible;

        // True once a loop cycle's fade has fully aged out and the ticker has parked instead of
        // replaying itself — see Park. Pens are held exactly where the fade left them (dark, ribbons
        // already faded to nothing), not returned to the pool, so a Show that follows can reuse them
        // with no pool Get/Return churn. Deciding WHAT draws next is not this ticker's job any more —
        // see the class remarks — so parking only ever reports the cycle is done via CycleComplete and
        // waits for ItemRangePreviewController to either replay it (Show) or let it go (BeginHide/Hide).
        // A ticker-level state, unrelated to Pen.Parked below (one pen waiting pen-up for its own
        // cascade window) beyond sharing the word for the same idea one level up: nothing is moving.
        private bool _parked;

        // Total arc length of _tracePoints, and the host's own position along it as an arc-length offset
        // from the trace's first point — both derived by BuildTraceBuffers alongside _traceArcTable above.
        // The trace runs from the projectile through the board and crosses the host somewhere along the
        // way, so the host offset is generally non-zero; together these anchor the approach cascade.
        private float _traceLength;
        private float _hostTraceOffset;

        // A graceful hide stops emitting but keeps the pens where they are, so what was already drawn
        // fades out over the ribbon's own lifetime instead of vanishing. _fadeDuration is that lifetime,
        // read off a pen's trail (see HighlightTrail.EffectiveRibbonSeconds) rather than authored again
        // here; _fadeElapsed tracks how far into it LateTick is. The very same machinery also drives the
        // re-bloom loop's own fade before each replay (BeginLoopFade) — the two only differ in what
        // AdvanceFade does once _fadeElapsed reaches _fadeDuration, decided by _cyclePhase below.
        private bool _fading;
        private float _fadeElapsed;
        private float _fadeDuration;

        // Captured on Show so LateTick doesn't re-resolve the config every frame.
        private float _dashSpacing;

        // The re-bloom loop's own state machine: Drawing while the cascade is still bristling in, Holding
        // through the settled pause, Fading once BeginLoopFade has stopped every pen and is waiting for
        // their ribbons to age out before Park reports the cycle complete (see CycleComplete). Explicit
        // rather than derived from one accumulated total against BloomDuration + RebloomHoldSeconds + a
        // fade duration — the fade's own length isn't even known until BeginLoopFade reads it off a pen,
        // so a single running total was never going to stay a readable comparison. Also doubles as the signal
        // AdvanceFade uses to tell the loop's own fade apart from a genuine BeginHide: only BeginLoopFade
        // ever leaves this at Fading. Reset to Drawing by Show, alongside _cycleElapsed, so a fresh figure
        // never starts mid-hold or mid-fade.
        private CyclePhase _cyclePhase;

        // Elapsed time within the current phase above — the single shared clock the re-bloom loop reads
        // (AdvanceRebloomCycle) rather than polling every pen for "am I settled yet," since every pen
        // already ticks against one shared BloomElapsed-derived clock by construction (BuildCascadeTimings
        // cuts every pen's window from the same BloomDuration). Reset on every phase transition, by Show,
        // and by a restart itself; only ever advances while visible and not fading, which LateTick already
        // gates before AdvanceRebloomCycle runs.
        private float _cycleElapsed;

        // The signal ItemRangePreviewController polls once per LateTick: true once a loop cycle has
        // parked (see Park/_parked). It stays true until the next Show clears it, which is what makes it
        // safe to read every frame rather than edge-triggering it — the controller calling Show in
        // response IS what consumes it; if nothing ever does, it just stays parked until a
        // BeginHide/Hide releases the pens instead, never observed twice for the one cycle that set it.
        internal bool CycleComplete => _parked;

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
        ///     True for a figure's first appearance on this host — pens cascade in one dash at a time: the
        ///     stroke's own dedicated approach pens draw the leg from the host origin to the stroke's entry
        ///     point one dash at a time, each retiring the instant its own dash is drawn, then the figure's
        ///     own dashes cascade from the entry point exactly as before; every dash-slot pen on the stroke
        ///     waits, parked pen-up, at its own dash's start until its turn, then draws that dash and hands
        ///     off to the next. False for a re-settle
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
        ///     never calls this on a host still actively visible — every call site (a fresh dwell, a
        ///     sequence advance, and a spin-driven re-bloom via <see cref="RequestEarlyCycleEnd" />) only
        ///     re-`Show`s once <c>_visible</c> has already dropped first, either via <see cref="BeginHide" />
        ///     (a real hide) or a completed loop cycle parking (see <see cref="CycleComplete" />) — so the
        ///     <see cref="RefitPens" /> branch below is genuinely a dormant fallback, not exercised by any
        ///     path today; <paramref name="introduce" /> is the mechanism that actually distinguishes the
        ///     two cases in practice, independently of whether the geometry itself is acquired or re-fitted.
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
            // reused for the figure being built below. When the fade being cancelled was a genuine
            // BeginHide, _visible is already false, so isSameHost comes out false regardless of slot,
            // which is what routes this into AcquirePens instead of RefitPens: the acquire path is what
            // re-blooms from the host, which is the intended fade-in. When it was instead the loop's own
            // fade still in flight (BeginLoopFade never touches _visible while the fade is running — see
            // Park), isSameHost can legitimately come out true for a Show on the very same host —
            // RefitPens' surviving-pen path stays correct for that case because StopPensEmitting already
            // keeps every pen's Emitting mirror in step with its trail's real state, so ApplyPenPosition
            // still sees a clean rising edge instead of a stale no-op.
            _fading = false;

            // A cycle that already finished parking (see Park) is likewise superseded here: clearing
            // this before isSameHost is computed below is what a controller-driven replay relies on —
            // Park already dropped _visible, so isSameHost reads false regardless of slot and this Show
            // takes the AcquirePens(introduce) path, never the dormant RefitPens one. Also the only place
            // CycleComplete is ever cleared, so a controller that observes it and calls back in here
            // cannot observe the same completed cycle twice.
            _parked = false;

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

            // A Show starts its own draw-in — resetting the phase and clock here, rather than leaving
            // either running, is what stops a fresh figure starting mid-hold or mid-fade, or a Show
            // racing an already-due re-bloom on the very same frame.
            _cyclePhase = CyclePhase.Drawing;
            _cycleElapsed = 0f;
            _visible = true;
        }

        internal void Hide()
        {
            // Teardown-immediate: whatever a fade might have been doing is moot once every pen is about
            // to be returned to the pool outright.
            _fading = false;

            // A parked ticker already reads !_visible with pens still held (see Park), so it would
            // otherwise slip past this guard unreleased — checked explicitly rather than folded into the
            // pen count below, since the defensive zero-pens park (BeginLoopFade) has none to count.
            if (!_visible && !_parked && _pens.Count == 0)
            {
                return;
            }

            ReleasePens();
            _visible = false;
            _parked = false;
        }

        /// <summary>
        ///     Releases nothing yet — stops every pen laying new ribbon and lets what's already drawn fade
        ///     on the <see cref="TrailRenderer" />'s own lifetime, then releases to the pool once that has
        ///     played out. Pens keep their positions while fading, so the figure holds its last shape
        ///     instead of collapsing to a point.
        /// </summary>
        internal void BeginHide()
        {
            // Called every frame the controller has nothing to show, so this must stay idempotent past
            // whichever frame actually starts something. A call arriving mid-fade is the one case that
            // still has to DO something: if that fade is the loop's own (see BeginLoopFade), a real hide
            // must win over it — the pens are already stopped and already ageing (StopPensEmitting), so
            // nothing about the fade itself restarts, only where it lands, redirected here from a park to
            // a release (see AdvanceFade). A call arriving mid- a genuine hide-fade, or after one has
            // already released, hits this same branch and is a true no-op, since _cyclePhase is already
            // Drawing and _visible already false.
            if (_fading)
            {
                _cyclePhase = CyclePhase.Drawing;
                _visible = false;
                return;
            }

            // A cycle that already finished parking has nothing left to fade — its ribbons are already
            // fully aged out (Park is only ever reached once the loop's own fade elapsed, or with nothing
            // to fade at all) — so there is no graceful stage to run here, only the release itself. Left
            // unhandled, a parked ticker would otherwise leak forever: it reads !_visible, so it would
            // fall straight through the guard below and never reach ReleasePens.
            if (_parked)
            {
                ReleasePens();
                _parked = false;
                return;
            }

            if (!_visible)
            {
                return;
            }

            if (_pens.Count == 0)
            {
                _visible = false;
                return;
            }

            StopPensEmitting();

            // Read off a live pen rather than re-authoring the duration here, so this can never drift
            // from the pen prefab's own authored ribbon lifetime.
            _fadeDuration = _pens[0].Trail != null ? _pens[0].Trail.EffectiveRibbonSeconds : 0f;
            _fadeElapsed = 0f;
            _fading = true;
            _visible = false;
        }

        /// <summary>
        ///     Ends the current figure's own re-bloom cycle early — the same fade-then-park a full
        ///     Draw+Hold cycle reaches on its own (see <see cref="AdvanceRebloomCycle" /> /
        ///     <see cref="BeginLoopFade" />), just triggered before <c>RebloomHoldSeconds</c> would have.
        /// </summary>
        /// <remarks>
        ///     Exists for <see cref="ItemRangePreviewController" />'s spin-driven re-bloom: a settled
        ///     rotation must never call <see cref="Show" /> on a figure that is still visible — that would
        ///     reposition its pens in place instead of re-blooming them (<see cref="RefitPens" />, not
        ///     <see cref="AcquirePens" />) — so it asks the cycle to end here instead and waits for
        ///     <see cref="CycleComplete" /> before drawing again, exactly like every other transition. Goes
        ///     through <see cref="BeginLoopFade" /> rather than <see cref="BeginHide" /> deliberately: this
        ///     figure is expected back very soon, which is what parking (not releasing) means, and is
        ///     exactly the contract the loop's own scheduled fade already carries — reusing it here means
        ///     there is still only one route to <see cref="Park" />.
        ///     <para>
        ///         Idempotent while a fade — this call's own or the loop's natural one — is already
        ///         running, and once parked, so the controller can call this every frame a settle remains
        ///         unconsumed without racing or restarting its own timer.
        ///     </para>
        /// </remarks>
        internal void RequestEarlyCycleEnd()
        {
            if (!_visible || _fading || _parked)
            {
                return;
            }

            BeginLoopFade();
        }

        // Stops every pen — figure and approach alike — laying new ribbon without releasing them, so
        // whatever's already drawn can age out on the ribbon's own lifetime instead of vanishing outright.
        // Shared by BeginHide (a real hide) and BeginLoopFade (the loop's own fade before a replay); the
        // two differ only in what happens once that ribbon lifetime elapses (see AdvanceFade). Also keeps
        // each pen's Emitting mirror in step with the trail it just stopped — not just the trail itself —
        // so a Show that later cancels this fade and lands on RefitPens' surviving-pen path (see Show)
        // still sees a real edge in ApplyPenPosition instead of a stale "already emitting" no-op.
        private void StopPensEmitting()
        {
            for (var i = 0; i < _pens.Count; i++)
            {
                var pen = _pens[i];
                if (pen.Trail != null)
                {
                    pen.Trail.SetEmitting(false);
                    pen.Emitting = false;
                }

                _pens[i] = pen;
            }

            // A retired approach pen is already not emitting, so this is a no-op for most of them — only
            // one still mid-dash (or not yet started) actually needs stopping here.
            for (var i = 0; i < _approachPens.Count; i++)
            {
                var pen = _approachPens[i];
                if (pen.Trail != null)
                {
                    pen.Trail.SetEmitting(false);
                    pen.Emitting = false;
                }

                _approachPens[i] = pen;
            }
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

            for (var i = 0; i < _approachPens.Count; i++)
            {
                var pen = _approachPens[i];
                AdvanceApproachPen(ref pen, deltaTime);
                _approachPens[i] = pen;
            }

            AdvanceRebloomCycle(deltaTime);
        }

        // The loop's own clock: ticks _cycleElapsed once per visible, non-fading frame — same gating
        // LateTick already applies before reaching here — and walks _cyclePhase through Drawing (the
        // draw-in, BloomDuration long) then Holding (the settled pause, RebloomHoldSeconds long), at which
        // point BeginLoopFade takes over: it stops every pen and moves the ticker into LateTick's other
        // branch (AdvanceFade), which is what actually calls Park and reports the cycle complete once the
        // fade elapses — replaying it, if anything does, is ItemRangePreviewController's call from there
        // (see CycleComplete). A single ticker-level clock rather than polling every pen for "am I
        // settled" — every pen already shares one BloomElapsed-derived clock by construction, so this
        // just reads the same shape of signal one level up. RebloomHoldSeconds <= 0 is the off switch:
        // never advance the phase at all, draw once and hold, exactly the behaviour before this existed —
        // and CycleComplete never turns true, since Park is only ever reached through this switch.
        private void AdvanceRebloomCycle(float deltaTime)
        {
            if (_config.RebloomHoldSeconds <= 0f)
            {
                return;
            }

            _cycleElapsed += deltaTime;

            switch (_cyclePhase)
            {
                case CyclePhase.Drawing:
                    if (_cycleElapsed >= _config.BloomDuration)
                    {
                        _cyclePhase = CyclePhase.Holding;
                        _cycleElapsed = 0f;
                    }

                    break;

                case CyclePhase.Holding:
                    if (_cycleElapsed >= _config.RebloomHoldSeconds)
                    {
                        BeginLoopFade();
                    }

                    break;
            }
        }

        // Starts the loop's own fade before a park — the settled figure stops emitting and its ribbons
        // age out on their own lifetime, exactly like a real hide (see StopPensEmitting), before Park
        // reports the cycle complete once AdvanceFade sees that lifetime elapse. Deliberately does NOT
        // touch _visible itself while the fade is running: the preview is still showing, just between
        // cycles, so the controller's own signature/dwell bookkeeping should keep reading it as shown
        // throughout the fade rather than spuriously treating a mid-loop fade as the aim having left. Only
        // once the fade actually completes does _visible drop — in Park, the same moment CycleComplete
        // turns true — mirroring how a genuine BeginHide always drops _visible before a re-Show. Two
        // callers now share this exact contract: AdvanceRebloomCycle, once the hold's own timer elapses,
        // and RequestEarlyCycleEnd, when a spin settling on a new angle asks for the same ending before
        // that timer would have — neither needs its own version of "stop, fade, park."
        private void BeginLoopFade()
        {
            if (_pens.Count == 0)
            {
                // Nothing to fade out — the cycle is complete the instant it would have started fading,
                // so park immediately rather than stalling on a fade with nothing to age out.
                Park();
                return;
            }

            StopPensEmitting();

            // Read off a live pen rather than re-authoring the duration here, so this can never drift
            // from the pen prefab's own authored ribbon lifetime — same source BeginHide reads.
            _fadeDuration = _pens[0].Trail != null ? _pens[0].Trail.EffectiveRibbonSeconds : 0f;
            _fadeElapsed = 0f;
            _fading = true;
            _cyclePhase = CyclePhase.Fading;
        }

        // Reached once a loop cycle's own fade has fully aged out (or BeginLoopFade found nothing to fade
        // at all): the ticker stops driving itself and reports the cycle complete instead of replaying —
        // deciding whether, and for what, to replay next is ItemRangePreviewController's job now, via
        // CycleComplete. Drops _visible here, not in BeginLoopFade, so the controller's own
        // signature/dwell bookkeeping keeps reading the figure as shown for the whole draw/hold/fade
        // arc and only sees it "gone" at the exact moment the pens actually go dark for good — the same
        // instant a Show responding to CycleComplete needs isSameHost to read false, so it takes the
        // AcquirePens(introduce) path (see Show) rather than the dormant RefitPens one, mirroring how a
        // genuine BeginHide already drops _visible before its own re-Show.
        private void Park()
        {
            _visible = false;
            _parked = true;
        }

        // Pens hold their last position while fading (no AdvancePen calls here) — a fading ribbon should
        // hang where it stopped, not keep sweeping its dash while it dims. Once the ribbon lifetime
        // elapses, _cyclePhase is what tells the loop's own fade (BeginLoopFade) apart from a genuine
        // hide (BeginHide) — only BeginLoopFade ever leaves it at Fading, so that's the one case that
        // parks instead of releasing outright.
        private void AdvanceFade(float deltaTime)
        {
            _fadeElapsed += deltaTime;
            if (_fadeElapsed < _fadeDuration)
            {
                return;
            }

            _fading = false;

            if (_cyclePhase == CyclePhase.Fading)
            {
                Park();
                return;
            }

            ReleasePens();
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

            // The offset alone is all this needs — the projected point itself matters only to Snipe's
            // corridor origin (SnipeRangePreview), which calls the same shared helper directly.
            ItemPreviewEntry.TryFindNearestPointOnPolyline(
                _tracePoints, new Vector3(_origin.x, _origin.y, 0f), out _, out _hostTraceOffset);
        }

        // Where each stroke's own figure-drawing starts: the arc-length offset of the point where the
        // aim's line of sight first crosses it, via the pure geometry helper. Falls back to the stroke's
        // own start (0) when the trace never crosses it at all — true of Lightning's later arcs, which the
        // trace only reaches through the chain, not directly; starting those at 0 makes each arc draw in
        // sequence after the one before it, which is the wanted look anyway rather than a special case.
        //
        // Also resolves that offset to a world point, and the arc-length distance the stroke's own approach
        // cascade actually covers: along the trace, between _hostTraceOffset and this stroke's own entry
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

        // Approach dashes on the same DashLength/DashSpacing stride as DeriveDashCounts, but sharing the
        // remaining budget after the figure's own dashes rather than the whole of MaxPens — the figure's
        // dashes are the payload and must win if the two compete, so figureTotal is subtracted first and
        // only what's left goes to the comet tail. A remaining budget of zero (or less) starves the
        // approach outright, not just to one dash each: a long approach must never crowd out the figure it
        // leads into. Reads _approachLengths, so callers must run this after BuildEntryOffsets, on both the
        // acquire and refit path — same reshaping reasons as DeriveDashCounts.
        private int DeriveApproachDashCounts(int remainingBudget)
        {
            var strokeCount = _shape.Strokes.Count;
            var stride = _config.DashLength + _config.DashSpacing;

            _approachDashesPerStroke.Clear();
            var desiredTotal = 0;
            for (var s = 0; s < strokeCount; s++)
            {
                var count = ApproachDashCountForLength(_approachLengths[s], stride);
                _approachDashesPerStroke.Add(count);
                desiredTotal += count;
            }

            if (remainingBudget <= 0)
            {
                for (var s = 0; s < strokeCount; s++)
                {
                    _approachDashesPerStroke[s] = 0;
                }

                return 0;
            }

            if (desiredTotal > remainingBudget && stride > 1e-5f)
            {
                var inflatedStride = stride * (desiredTotal / (float)remainingBudget);
                desiredTotal = 0;
                for (var s = 0; s < strokeCount; s++)
                {
                    var count = ApproachDashCountForLength(_approachLengths[s], inflatedStride);
                    _approachDashesPerStroke[s] = count;
                    desiredTotal += count;
                }
            }

            return desiredTotal;
        }

        // Zero comet dashes is a fully supported outcome, not a degenerate one rounded up to one — true
        // both for a near-zero approach (the entry sits on the host itself — Paint's apex, Lightning's
        // first arc, Laser and Snipe's whole line) and for one merely too short to carry a full dash. The
        // honest floor is one whole stride: an approach that can't fit DashLength + DashSpacing has no
        // room for a dash whose ComputePaintedLength wouldn't immediately floor to slot * 0.1f — a sliver
        // that reads as a point, not a dash. Measured against whatever stride the caller passes in
        // (authored or budget-inflated by DeriveApproachDashCounts), so a tighter budget raises this floor
        // exactly as it should: fewer pens to spend means a shorter approach is worth spending one on.
        // internal rather than private so the pure sweep of thresholds/counts is edit-mode testable
        // without the ticker's pen/pool machinery.
        internal static int ApproachDashCountForLength(float approachLength, float stride)
        {
            if (stride <= 1e-5f)
            {
                return approachLength > 1e-4f ? 1 : 0;
            }

            return approachLength <= stride ? 0 : Mathf.Max(1, Mathf.RoundToInt(approachLength / stride));
        }

        // The arc length a pen actually paints within its slot, factored out so BuildCascadeTimings' split
        // and AdvanceDash's own ping-pong always agree on the identical value — see AdvanceDash for why the
        // gap, not the dash, is the pinned quantity.
        private float ComputePaintedLength(float slotLength)
        {
            return Mathf.Max(slotLength * 0.1f, slotLength - _dashSpacing);
        }

        // Splits BloomDuration into this stroke's own cascade windows: the approach cascade gets a share
        // proportional to its length against the stroke's total path (approach length + every dash's own
        // painted length), and the remainder divides evenly across the stroke's figure dash count, so pen
        // k's window starts exactly when pen k − 1's ends (AdvanceCascade). Reads _dashesPerStroke,
        // _approachLengths and _approachDashesPerStroke, so callers must run this after DeriveDashCounts,
        // BuildEntryOffsets and DeriveApproachDashCounts, on both the acquire and the refit path — a
        // re-fit can change a stroke's dash count, entry offset or approach dash count, and the windows
        // would otherwise go stale against the new geometry.
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

                // No approach pens on this stroke — the entry sits on the host itself (Paint's apex,
                // Lightning's first arc), the leg is shorter than one dash stride
                // (ApproachDashCountForLength), or the shared MaxPens budget starved it — means nothing
                // would be drawn during an approach window, so the stroke gets none: its figure dashes
                // cascade across the whole BloomDuration and start at t = 0, instead of waiting out a
                // window with no comet leading into it. Gated on the actual pen count rather than just
                // approachLength, since a stroke can carry a real approachLength yet own zero approach
                // pens under either case above.
                var approachDuration = _approachDashesPerStroke[s] > 0 && total > 1e-4f
                    ? bloomDuration * (approachLength / total)
                    : 0f;

                _approachDurations.Add(approachDuration);
                _dashDurations.Add((bloomDuration - approachDuration) / dashesOnStroke);
            }
        }

        // Shared by Hide() and a fade's natural completion — the two moments every pen actually goes back
        // to the pool, as opposed to BeginHide, which only stops them emitting. Releases both pools: a
        // retired approach pen is still allocated (see the class remarks on _approachPens) right up until
        // this runs.
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

            for (var i = 0; i < _approachPens.Count; i++)
            {
                var trail = _approachPens[i].Trail;
                if (trail != null)
                {
                    _poolManager.Return(_poolKey, trail);
                }
            }

            _approachPens.Clear();
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

        // Mirrors ResizePens for the approach-dash pool — a separate list (see _approachPens) so it needs
        // its own resize rather than sharing the figure's.
        private void ResizeApproachPens(int wanted)
        {
            while (_approachPens.Count > wanted)
            {
                var last = _approachPens.Count - 1;
                var trail = _approachPens[last].Trail;
                if (trail != null)
                {
                    _poolManager.Return(_poolKey, trail);
                }

                _approachPens.RemoveAt(last);
            }

            while (_approachPens.Count < wanted)
            {
                _approachPens.Add(new ApproachPen());
            }
        }

        // The figure-pen reset for the start of a draw-in — parked pen-up at the host origin, cascade
        // state zeroed. Used by AcquirePens' introduce branch alone now — both a figure's first
        // appearance and a loop's replay (ItemRangePreviewController re-Showing once CycleComplete fires)
        // go through it, so "what does it mean for this pen to begin cascading in" has exactly one
        // definition rather than a second copy that could drift from the original. Never seeds the pen
        // emitting: AdvancePen's rising edge is what starts the ribbon, once this pen's own cascade
        // window opens, never this reset directly —
        // the standing rule that a pen must never be emitting at a position it is about to leave
        // discontinuously is exactly why the trail goes dark here rather than mid-sweep.
        private void ResetPenForDrawIn(ref Pen pen)
        {
            pen.BloomElapsed = 0f;
            pen.Bloomed = false;
            pen.Parked = true;
            pen.Distance = 0f;

            pen.Trail.ClearRibbon();
            pen.Trail.SetPosition(new Vector3(_origin.x, _origin.y, 0f));
            pen.Trail.SetEmitting(false);
            pen.Emitting = false;

            pen.HasLastPosition = false;
            pen.LastPosition = default;
        }

        // Mirrors ResetPenForDrawIn one level down, for an approach dash's own pen — used by
        // AcquireApproachPens' introduce branch alone now, on both a figure's first appearance and a
        // loop's replay. A retired approach pen (the common case once a settled hold elapses) is revived
        // here, not just reset: Retired must go back to false or AdvanceApproachPen would skip it forever.
        private void ResetApproachPenForDrawIn(ref ApproachPen pen)
        {
            pen.Retired = false;
            pen.Emitting = false;
            pen.BloomElapsed = 0f;

            pen.Trail.ClearRibbon();
            pen.Trail.SetPosition(new Vector3(_origin.x, _origin.y, 0f));
            pen.Trail.SetEmitting(false);
        }

        private void AcquirePens(bool introduce)
        {
            var strokeCount = _shape.Strokes.Count;
            var wanted = DeriveDashCounts();

            // Figure dashes claim their share of MaxPens first — see DeriveApproachDashCounts for why the
            // approach only ever gets what's left over. Must run before BuildCascadeTimings, which now
            // reads the resulting _approachDashesPerStroke to decide whether a stroke gets an approach
            // window at all.
            var approachWanted = DeriveApproachDashCounts(_config.MaxPens - wanted);
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

                    if (introduce)
                    {
                        ResetPenForDrawIn(ref pen);
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
                        //
                        // Distance only matters once Bloomed (AdvanceDash's own ping-pong) — the cascade
                        // itself is driven by the shared clock in AdvanceCascade, not by accumulated
                        // distance, so this just primes the value AdvanceDash will inherit at handoff.
                        pen.Distance = 0f;
                        pen.BloomElapsed = _config.BloomDuration + 1f;
                        pen.Bloomed = true;
                        pen.Parked = false;

                        // Parked pen-up at the origin rather than seeded emitting: AdvancePen's rising
                        // edge (visible flips true) is what starts the ribbon, and that edge clears first
                        // and runs after SetPosition, so the ribbon opens clean at the pen's real first
                        // position — wherever this pen's own dash start puts it once its window opens —
                        // never a chord from this parked origin to there.
                        pen.Trail.ClearRibbon();
                        pen.Trail.SetPosition(new Vector3(_origin.x, _origin.y, 0f));
                        pen.Trail.SetEmitting(false);
                        pen.Emitting = false;

                        // A reused pen's teleport baseline is stale from its previous figure, and a
                        // freshly grown one already reads false — either way it must not be trusted, so
                        // clear it explicitly rather than rely on carryover.
                        pen.HasLastPosition = false;
                        pen.LastPosition = default;
                    }

                    _pens[penIndex] = pen;
                    penIndex++;
                }

                AssignCascadeRanks(s, strokeStartIndex, dashesOnStroke, slotLength);
            }

            AcquireApproachPens(approachWanted, introduce);
        }

        // The approach cascade's own acquire pass, mirroring AcquirePens but for the comet's tail: one pen
        // per approach dash, in host-to-entry order (DashIndex already IS that order — the approach path
        // is a single arc from the host, unlike a stroke, so there's no separate ranking step to run).
        // introduce = false (a re-settle on an already-telegraphed host) reappears in place exactly like a
        // figure dash does, just adapted for a pen that retires instead of settling into a loop — see
        // SettleApproachPen.
        private void AcquireApproachPens(int wanted, bool introduce)
        {
            ResizeApproachPens(wanted);

            var penIndex = 0;
            for (var s = 0; s < _shape.Strokes.Count; s++)
            {
                var dashesOnStroke = _approachDashesPerStroke[s];
                for (var dashIndex = 0; dashIndex < dashesOnStroke; dashIndex++)
                {
                    var pen = _approachPens[penIndex];
                    pen.Trail ??= _poolManager.GetOrRegister(
                        _poolKey, () => new SimplePoolChannel<HighlightTrail>(_penPrefab));
                    pen.StrokeIndex = s;
                    pen.DashIndex = dashIndex;

                    if (introduce)
                    {
                        // Parked pen-up at the origin — a placeholder AdvanceApproachPen overwrites with
                        // the pen's real dash-start position before it is ever emitting, the same rule
                        // AcquirePens follows for the figure's own pens.
                        ResetApproachPenForDrawIn(ref pen);
                    }
                    else
                    {
                        SettleApproachPen(ref pen, s, dashIndex);
                    }

                    _approachPens[penIndex] = pen;
                    penIndex++;
                }
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

            // Same ordering constraint as AcquirePens: BuildCascadeTimings reads _approachDashesPerStroke.
            var approachWanted = DeriveApproachDashCounts(_config.MaxPens - wanted);
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

            RefitApproachPens(approachWanted);
        }

        // The approach cascade's own refit pass, mirroring RefitPens: a surviving pen (Trail already set)
        // just gets reassigned to its (possibly different) dash slot and keeps whatever progress or
        // retirement it already had — a re-fit re-deriving the approach's own length or dash count under
        // it is the same tolerated staleness RefitPens already accepts for the figure's dashes. A pen the
        // approach only just grew into is handed the fully-drawn, retired state directly, exactly as
        // RefitPens does for a new figure-dash pen.
        private void RefitApproachPens(int wanted)
        {
            ResizeApproachPens(wanted);

            var penIndex = 0;
            for (var s = 0; s < _shape.Strokes.Count; s++)
            {
                var dashesOnStroke = _approachDashesPerStroke[s];
                for (var dashIndex = 0; dashIndex < dashesOnStroke; dashIndex++)
                {
                    var pen = _approachPens[penIndex];
                    var isNewPen = pen.Trail == null;
                    pen.Trail ??= _poolManager.GetOrRegister(
                        _poolKey, () => new SimplePoolChannel<HighlightTrail>(_penPrefab));
                    pen.StrokeIndex = s;
                    pen.DashIndex = dashIndex;

                    if (isNewPen)
                    {
                        SettleApproachPen(ref pen, s, dashIndex);
                    }

                    _approachPens[penIndex] = pen;
                    penIndex++;
                }
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
        // CascadeRank, so AdvanceCascade knows each pen's turn in the relay — rank 0 is simply the first
        // figure dash to draw, once the stroke's own approach cascade (a separate set of pens; see
        // AdvanceApproachPen) has finished. A plain O(n²) compare is fine here: it runs once per
        // Show/refit, never per frame, and n is bounded by MaxPens.
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

        // Dispatches to whichever phase this pen is in — cascading in (waiting its turn, or sweeping its
        // own dash for the first time), or (once Bloomed) settled into its dash slot's own ping-pong — then
        // applies the result uniformly (teleport guard, positioning, visibility-driven emit) regardless of
        // which phase produced it.
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
        // No figure pen ever leaves its own dash slot any more — the approach leg belongs entirely to a
        // separate set of pens now (AdvanceApproachPen), one per approach dash, that draw it and retire.
        // Every figure pen, rank 0 included, simply waits Parked and pen-up at its own dash's start until
        // its window opens — rank 0's window starts exactly at approachDuration, i.e. the moment the
        // approach cascade's own last window closes, so the figure's first dash begins the instant the
        // comet's tail reaches the entry point.
        private Vector3 AdvanceCascade(ref Pen pen, float deltaTime)
        {
            pen.BloomElapsed += deltaTime;

            var duration = Mathf.Max(_config.BloomDuration, 1e-4f);
            var t = Mathf.Clamp01(pen.BloomElapsed / duration);
            var elapsed = _config.BloomCurve.Evaluate(t) * _config.BloomDuration;

            var strokeIndex = pen.StrokeIndex;
            var approachDuration = _approachDurations[strokeIndex];
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
            // Gated on Bloomed because mid-cascade the pen can deliberately move far and fast — a waiting
            // pen's jump from its parked spot into its own dash the instant its window opens — easily
            // outrunning this threshold every frame; checking there would clear the ribbon continuously
            // and suppress the draw-in entirely.
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
            // its dash in. A pen stays dark only while Parked: waiting at its own dash start until its
            // turn, reached by a discontinuous jump it must not reveal. Visibility decides the edge the
            // rest of the time.
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

        // The approach leg itself: sweeps along the trace polyline between the host's own position on it
        // (_hostTraceOffset) and this stroke's entry point (_entryTraceOffsets), so a bent aim line — a
        // wall bounce, a deflection — is followed rather than cut across in a straight line. Shield is the
        // figure this matters most for, since its entry sits at the far end of the aim line, but it is a
        // general fix: any figure whose entry isn't adjacent to the host bends with the trace now. The two
        // offsets can fall in either order (the entry can sit behind the host along the trace) — Lerp
        // moves continuously from one to the other regardless of which is larger, so both directions just
        // work. Falls back to the straight host-to-entry lerp used before this existed when the trace
        // can't answer for this stroke (BuildEntryOffsets already decided that per stroke). t spans the
        // whole leg; SampleApproachArc below is what an individual approach pen actually calls, converting
        // its own dash's arc-length range into this function's t.
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

        // Converts an arc-length offset along the approach leg (0 at the host, approachLength at the
        // entry) into SampleApproach's own t — a fraction of the leg — so an approach pen can be positioned
        // by arc length like every other pen in the file, rather than the ticker growing a second position
        // convention just for this cascade.
        private Vector3 SampleApproachArc(int strokeIndex, float approachLength, float arcOffset)
        {
            var t = approachLength > 1e-4f ? Mathf.Clamp01(arcOffset / approachLength) : 0f;
            return SampleApproach(strokeIndex, t);
        }

        // The arc-length range this approach dash paints — on the same DashLength/DashSpacing stride and
        // ComputePaintedLength gap-pinning as a figure dash — factored out since both the live sweep
        // (AdvanceApproachPen) and the instant re-settle (SettleApproachPen) need the identical geometry.
        // dashesOnStroke is always at least 1 at every call site (both loop within dashIndex <
        // dashesOnStroke), so the division below can't land on a zero approach dash count.
        private void ComputeApproachDashArc(
            int strokeIndex, int dashIndex, out float approachLength, out float dashStartArc, out float painted)
        {
            var dashesOnStroke = _approachDashesPerStroke[strokeIndex];
            approachLength = _approachLengths[strokeIndex];
            var slotLength = approachLength / dashesOnStroke;
            painted = ComputePaintedLength(slotLength);
            dashStartArc = dashIndex * slotLength;
        }

        // A re-settled approach pen (introduce == false in AcquireApproachPens, or a pen the approach only
        // just grew into under RefitApproachPens) has no cascade to relay through — it should simply
        // already be drawn, exactly as a re-settled figure dash already sits mid-ping-pong instead of
        // cascading in. Unlike a figure dash there's no steady-state loop to drop it into, so this paints
        // the dash directly: start position, emitting on, end position, emitting off — one clean segment,
        // then retired, aging on the ribbon's own TrailRenderer time like any other retired approach pen.
        private void SettleApproachPen(ref ApproachPen pen, int strokeIndex, int dashIndex)
        {
            ComputeApproachDashArc(strokeIndex, dashIndex, out var approachLength, out var dashStartArc, out var painted);

            var start = SampleApproachArc(strokeIndex, approachLength, dashStartArc);
            var end = SampleApproachArc(strokeIndex, approachLength, dashStartArc + painted);

            pen.Trail.ClearRibbon();
            pen.Trail.SetPosition(start);
            pen.Trail.SetEmitting(true);
            pen.Trail.SetPosition(end);
            pen.Trail.SetEmitting(false);

            pen.Emitting = false;
            pen.Retired = true;
            pen.BloomElapsed = _config.BloomDuration + 1f;
        }

        // Positions and emits one approach pen — the visibility half of ApplyPenPosition's job, minus the
        // teleport guard: an approach pen never gets reassigned mid-flight the way a figure dash can under
        // RefitPens (see RefitApproachPens), so there is no discontinuous jump here to guard against beyond
        // the rising-edge clear every pen in the file already does.
        private void ApplyApproachPenPosition(ref ApproachPen pen, Vector3 position, bool wantsToEmit)
        {
            pen.Trail.SetPosition(position);

            var visible = wantsToEmit && (!_viewport.IsActive || _viewport.Contains(position));
            if (visible == pen.Emitting)
            {
                return;
            }

            if (visible)
            {
                pen.Trail.ClearRibbon();
            }

            pen.Trail.SetEmitting(visible);
            pen.Emitting = visible;
        }

        // The comet's tail: mirrors AdvanceCascade's parked/drawing split for a single approach dash, but
        // a completed one RETIRES instead of settling into any further phase — see the class remarks on
        // _approachPens for why a single pen can't produce this tail by toggling emitting on its own.
        // Retiring is what produces the fade: a stationary ribbon ages out purely on its own TrailRenderer
        // time (no per-pen colour or alpha involved, deliberately), so the dash drawn earliest — nearest
        // the item — is also the one that has been aging the longest once pens further out are still
        // advancing or haven't started. Skips entirely once Retired, so a finished approach pen costs
        // nothing more per frame until the next Show.
        private void AdvanceApproachPen(ref ApproachPen pen, float deltaTime)
        {
            if (pen.Trail == null || pen.Retired)
            {
                return;
            }

            // This pen's own local clock is the only stand-in this file has for "how far along the
            // approach cascade is" — every pen on a stroke starts it at 0 and ticks it by the same
            // deltaTime every frame, so any one of them is as good a proxy for the shared window timing
            // as any other.
            pen.BloomElapsed += deltaTime;

            var duration = Mathf.Max(_config.BloomDuration, 1e-4f);
            var t = Mathf.Clamp01(pen.BloomElapsed / duration);
            var elapsed = _config.BloomCurve.Evaluate(t) * _config.BloomDuration;

            var strokeIndex = pen.StrokeIndex;
            ComputeApproachDashArc(strokeIndex, pen.DashIndex, out var approachLength, out var dashStartArc, out var painted);

            var dashesOnStroke = _approachDashesPerStroke[strokeIndex];
            var approachDuration = _approachDurations[strokeIndex];
            var dashDuration = dashesOnStroke > 0 ? approachDuration / dashesOnStroke : 0f;
            var windowStart = pen.DashIndex * dashDuration;

            if (elapsed < windowStart)
            {
                // Not yet its turn — sits pen-up at its own dash's start point. Never emitting here, so
                // the rising edge below is what lets it start drawing without chording from wherever it
                // was, the same rule every pen in this file follows.
                ApplyApproachPenPosition(ref pen, SampleApproachArc(strokeIndex, approachLength, dashStartArc), false);
                return;
            }

            // t < 1f alongside the window-end comparison is cheap insurance against the same
            // saturating-clock class of bug AdvanceCascade's own promotion guard exists for: elapsed is
            // Clamp01'd t run through BloomCurve, so if it ever failed to round-trip past windowStart +
            // dashDuration, the pen would stall here mid-sweep instead of retiring. Once t itself saturates
            // at 1 there is no further elapsed to wait on, so this forces the fall-through unconditionally.
            if (dashDuration > 1e-4f && t < 1f && elapsed < windowStart + dashDuration)
            {
                var localT = (elapsed - windowStart) / dashDuration;
                var position = SampleApproachArc(strokeIndex, approachLength, dashStartArc + (localT * painted));
                ApplyApproachPenPosition(ref pen, position, true);
                return;
            }

            // The sweep just finished (or dashDuration was degenerate, or t saturated) — retire pen-up
            // for good.
            RetireApproachPen(ref pen, strokeIndex, approachLength, dashStartArc, painted);
        }

        // Window closed: retire exactly at the dash's own end, pen-up for good. No promotion to any
        // further phase — this pen is done drawing until the next Show re-acquires it.
        private void RetireApproachPen(
            ref ApproachPen pen, int strokeIndex, float approachLength, float dashStartArc, float painted)
        {
            var dashFrontArc = dashStartArc + painted;
            ApplyApproachPenPosition(ref pen, SampleApproachArc(strokeIndex, approachLength, dashFrontArc), false);
            pen.Retired = true;
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
            var dashStart = pen.DashIndex * slotLength;

            if (slotLength <= 1e-5f)
            {
                return SampleStroke(pen.StrokeIndex, dashStart);
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
            return SampleStroke(pen.StrokeIndex, dashStart + withinDash);
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

        // The re-bloom loop's three-phase state — see _cyclePhase and AdvanceRebloomCycle for what drives
        // the transitions between them.
        private enum CyclePhase
        {
            Drawing,
            Holding,
            Fading,
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
            // Every figure pen is Parked through the whole approach now, rank 0 included — the leg itself
            // belongs to a separate set of pens that draw and retire (see ApproachPen below). Set each tick
            // by AdvanceCascade and read by ApplyPenPosition to decide the dark/lit edge — recorded on the
            // pen rather than recomputed in both places.
            public bool Parked;

            // Mirrors the trail's own emitting flag, so AdvancePen only calls into the renderer on a real
            // edge (bloom settling, or crossing the visible-rect boundary) instead of every frame.
            public bool Emitting;

            // The slot this pen owns for its whole life. One pen draws one dash — the dashed line is the
            // pens sitting side by side, not one pen visiting every slot.
            public int DashIndex;

            // This pen's position in its stroke's relay order — 0 draws first, once the stroke's own
            // approach cascade has finished, 1 draws next once rank 0's window closes, and so on. Computed
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

        // One approach dash's own pen — the comet's tail. Simpler than Pen: no CascadeRank, teleport-guard,
        // or Distance/ping-pong fields, since an approach pen sweeps its own dash exactly once and then
        // retires for good — it never settles into a loop the way a Bloomed figure dash does, and it is
        // never reassigned to a different dash mid-flight the way a figure pen can be under a refit.
        private struct ApproachPen
        {
            public HighlightTrail Trail;
            public int StrokeIndex;
            public float BloomElapsed;

            // The slot this pen owns for its whole life — also its place in the host-to-entry relay order,
            // since the approach is a single arc from the host rather than a stroke with its own entry
            // point to rank against, so no separate AssignCascadeRanks-style step is needed here.
            public int DashIndex;

            // Mirrors the trail's own emitting flag, so AdvanceApproachPen only calls into the renderer on
            // a real edge, exactly as Pen.Emitting does for a figure dash.
            public bool Emitting;

            // True once this pen has drawn its own dash and gone pen-up for good — never re-emits, never
            // moves again, and AdvanceApproachPen skips it outright from then on. This is the field Pen
            // has no equivalent of: a figure dash settles into AdvanceDash's ping-pong forever instead,
            // but a single pen toggling emitting on and off would chord across every gap it tried to leave
            // (see the class remarks on _approachPens), so an approach pen must stop for good rather than
            // loop indefinitely. The pen stays allocated — not returned to the pool — until the next Show,
            // so its ribbon can keep fading on the TrailRenderer's own time instead of despawning mid-fade.
            public bool Retired;
        }
    }
}
