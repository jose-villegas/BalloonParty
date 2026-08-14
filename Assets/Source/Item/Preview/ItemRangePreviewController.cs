using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Items;
using BalloonParty.Prediction;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Decides which item hosts the aim is sighted on, and drives the visible range telegraph through
    ///     them in sequence, first-to-last along the line.
    /// </summary>
    /// <remarks>
    ///     Plain C# rather than a component on the balloon prefab, for two reasons. It needs
    ///     <see cref="SlotGrid" />, the item config and the pool — DI singletons that a pooled item visual
    ///     (hand-threaded by <c>ItemDisplayService</c>, never resolver-spawned) would have to receive
    ///     one by one. And only ONE preview shows at a time, which is a global arbitration a per-balloon
    ///     component cannot make without every instance knowing about every other.
    ///     <para>
    ///         <c>PredictionSightProbe</c> is untouched and independent: it drives the per-item visual
    ///         REACTIONS on the icon itself (glitter, fade, drift). This answers a different question — which
    ///         host currently owns the board-level figure — and shares only the underlying
    ///         <see cref="TraceHitGeometry" /> test.
    ///     </para>
    /// </remarks>
    internal sealed class ItemRangePreviewController : IStartable, ILateTickable, IDisposable
    {
        // How far the figure's tracked inputs may drift between frames and still count as "the same
        // aim" — exists purely to absorb float jitter in a held aim (position/normal noise a few ULPs
        // wide), not to tune how forgiving the settled check feels. Vector comparisons use the squared
        // form against SignatureEpsilonSq to avoid a sqrt; the degrees comparison below uses the bare
        // value since it isn't a vector.
        private const float SignatureEpsilon = 1e-3f;
        private const float SignatureEpsilonSq = SignatureEpsilon * SignatureEpsilon;

        private readonly SlotGrid _grid;
        private readonly PredictionTraceProvider _traceProvider;
        private readonly ItemPreviewTicker _ticker;
        private readonly ItemPreviewViewport _viewport;
        private readonly IItemPreviewConfig _config;
        private readonly IEnumerable<IItemRangePreview> _previews;

        // Every item host the trace currently crosses, ordered first-to-last along the line via
        // ItemPreviewSightOrder — the sequence the telegraph steps through. Rebuilt from scratch by
        // RefreshSightedHosts on every grid-walk refresh, never appended across frames.
        private readonly List<ItemPreviewSightedHost> _sightedHosts = new();

        // Ordered slots of the set the current signature describes, and of the set last actually shown —
        // parallel snapshots of _sightedHosts' own Slot column (via CopySlots). Two separate lists, not
        // one, because they answer two different questions on two different cadences: _signatureSlots is
        // compared every frame to decide whether the aim moved to a genuinely different set of balloons
        // (HasSetChanged); _shownSetSlots is compared only when a dwell elapses, to decide whether that
        // set is the very one already on screen (ShowActiveHost's introduce rule).
        private readonly List<Vector2Int> _signatureSlots = new();
        private readonly List<Vector2Int> _shownSetSlots = new();

        private Dictionary<ItemType, IItemRangePreview> _previewMap;
        private int _lastVersion = int.MinValue;

        private bool _hasSightedHosts;

        // Which entry in _sightedHosts is currently being drawn (or about to be, once dwell elapses).
        // Reset to 0 only when the SET itself changes (see LateTick) — advancing through an unchanged set
        // via AdvanceSequence is not a signature change and must not restart the sequence.
        private int _sequenceIndex;

        // The active host's inputs as of the last frame the signature was checked, compared against this
        // frame's to decide whether the aim actually moved. NOT PredictionTraceProvider.Version — that
        // increments every Tick while aiming (ThrowerController.UpdatePredictionTrace calls SetTrace
        // unconditionally) regardless of whether the aim moved, so it can't stand in for this.
        // _signatureSlots (above) is the SET half of the signature; these four are the active host's own
        // continuous geometry, updated both when the set changes (StoreSignature) and when the sequence
        // advances to a new active host (AdvanceSequence) — never when neither happens, which is what lets
        // a drifting balloon still invalidate a held signature without every advance doing the same. Spin
        // is deliberately NOT one of these — see _previousSpinSettled below for why it is tracked apart
        // from this signature instead of inside it.
        private bool _hasSignature;
        private Vector2 _signatureOrigin;
        private Vector2 _signatureDirection;
        private PredictionTraceEndKind _signatureTraceKind;
        private Vector2 _signatureTraceNormal;

        // Whether the figure for the current (stable) signature has already been shown — gates Show to
        // once per signature, since calling it again is exactly what would reposition a visible figure's
        // pens.
        private bool _shown;
        private float _dwellElapsed;

        // How long the current sequence entry (_sequenceIndex) has been the active host — ticks every
        // frame regardless of what's on screen, but only actually RESET once that host is actually shown:
        // ShowActiveHost (a dwell just elapsed) or AdvanceSequence (a fresh host takes over) — NEVER by a
        // spin-driven re-bloom in place (see AdvanceOrRebloomActiveHost), and deliberately not at the
        // signature change that starts a dwell either. Resetting at the actual draw rather than the
        // signature change is what keeps SightDelaySeconds from silently eating into the turn budget: the
        // dwell is time the host isn't on screen yet, so it must not count against how long it gets to
        // stay once it is. Exists because Show always restarts ItemPreviewTicker's own draw/hold/fade
        // clock (see Show's remarks), so a host whose spin re-blooms it faster than a full cycle would
        // otherwise never let ItemPreviewTicker.CycleComplete fire on its own — this is the fallback that
        // still ends its turn once it has run that long anyway.
        private float _activeHostElapsed;

        // Set by AdvanceSequence when the host it just advanced onto is mid-transition and its draw had
        // to be deferred (see ShowHost) — the sequence index has already moved, but nothing was drawn, so
        // _activeHostElapsed is left stale (still at or past oneCycleSeconds, which is what triggered the
        // advance in the first place). AdvanceOrRebloomActiveHost checks this before running its normal
        // turn-timer decision: without it, that stale elapsed value would immediately look like ANOTHER
        // turn already expired and advance straight past this host, unseen. Cleared on the retry's
        // successful draw, and on any full reset or signature change, so a stale flag can never make an
        // unrelated host's turn-timer check take the wrong branch.
        private bool _activeHostDrawPending;

        // The active host's ISpinningItemVisual.IsSettled as of last frame, read straight off
        // LaserItemRotation.IsDwelling (via IHostsSpinningItem) — an exact boolean, not a threshold on a
        // lerped angle, so this is a genuine edge detector rather than the frame-delta heuristics this
        // area used to lean on. HasSpinLeftSettledAngle compares it against this frame's reading to catch
        // the FALLING edge: the rotation leaving its settled step and beginning a transition. That is now
        // the re-bloom trigger, not arrival — see HasSpinLeftSettledAngle for why firing on arrival left
        // the figure showing an orientation the laser had already turned away from. _hasPreviousSpinSettled
        // guards the first frame after any (re)draw — reset inside ShowHost, called from ShowActiveHost,
        // AdvanceSequence and the CycleComplete re-bloom-in-place branch of AdvanceOrRebloomActiveHost —
        // so a newly active host's first reading is never mistaken for an edge.
        private bool _previousSpinSettled;
        private bool _hasPreviousSpinSettled;

        // True once HasSpinLeftSettledAngle has asked the ticker to end the current cycle early, for the
        // cycle currently in flight — the only way AdvanceOrRebloomActiveHost's CycleComplete branch can
        // tell "this park is the rotation's own falling edge" apart from "the hold loop's timer reached
        // RebloomHoldSeconds on its own," since both funnel into the very same CycleComplete signal (see
        // ItemPreviewTicker.RequestEarlyCycleEnd/CycleComplete remarks). Reset on every draw by ShowHost —
        // the shared funnel, same as _hasPreviousSpinSettled — so a stale request from a previous host or
        // a previous cycle can never be read as covering this one.
        private bool _earlyCycleEndRequested;

        // Whether the SET last actually shown (_shownSetSlots) is still current — see ShowActiveHost for
        // why re-settling on the identical set skips the re-bloom, and RefreshSightedHosts for why a
        // detour through a different set must not be invisible to that check.
        private bool _hasShownSet;

        internal ItemRangePreviewController(
            SlotGrid grid,
            PredictionTraceProvider traceProvider,
            ItemPreviewTicker ticker,
            ItemPreviewViewport viewport,
            IItemPreviewConfig config,
            IEnumerable<IItemRangePreview> previews)
        {
            _grid = grid;
            _traceProvider = traceProvider;
            _ticker = ticker;
            _viewport = viewport;
            _config = config;
            _previews = previews;
        }

        public void Start()
        {
            // Built once from the registered implementations, exactly as ItemActivator maps IBalloonItem —
            // a new item's preview is picked up by registering it, with no table to edit here.
            _previewMap = new Dictionary<ItemType, IItemRangePreview>();
            foreach (var preview in _previews)
            {
                _previewMap[preview.Type] = preview;
            }
        }

        public void Dispose()
        {
            // Immediate, not graceful — nothing is left running past the controller's own life to fade
            // pens the pool no longer has a ticker driving.
            _ticker.Hide();
        }

        public void LateTick()
        {
            // Idempotent, so calling it here before the ticker's own LateTick call costs nothing extra —
            // this just guarantees the viewport is fresh before the context below is built from it.
            _viewport.Refresh();

            if (!_traceProvider.IsActive || _traceProvider.Points.Count < 2)
            {
                _lastVersion = int.MinValue;
                HideAndClearSignature();
                return;
            }

            // Gated on the trace version, which in practice never skips this while aiming — Version ticks
            // every Tick regardless of whether the aim moved (see the signature fields' remark above), so
            // this gate saves nothing today. Left as-is: fixing the grid walk running every frame is a
            // separate, known issue, not something this change is meant to address.
            if (_traceProvider.Version != _lastVersion)
            {
                _lastVersion = _traceProvider.Version;
                RefreshSightedHosts();
            }

            if (!_hasSightedHosts)
            {
                HideAndClearSignature();
                return;
            }

            // A changed SET restarts the sequence at its first host; an unchanged one keeps whatever
            // AdvanceSequence last left it at. Computed before reading _sightedHosts[_sequenceIndex] below
            // so a set that shrank since the last frame can never index past its new end.
            // _activeHostElapsed is deliberately NOT reset here — see that field's remarks for why it
            // waits for the host to actually be shown instead.
            var setChanged = !_hasSignature || HasSetChanged();
            if (setChanged)
            {
                _sequenceIndex = 0;
            }

            var active = _sightedHosts[_sequenceIndex];
            var spinDegrees = ResolveSpinDegrees(active.Slot, out var spinSettled, out var spinning);
            var traceEnd = _traceProvider.End;

            // A changed signature (a different set, or the active host's own geometry drifting — NOT its
            // spin, see HasActiveSignatureChanged) hides gracefully and restarts the dwell — but falls
            // through to the accumulate-and-check below instead of returning, so a SightDelaySeconds of 0
            // still shows on this same frame rather than costing one.
            if (setChanged || HasActiveSignatureChanged(active, in traceEnd))
            {
                _ticker.BeginHide();
                _shown = false;
                _dwellElapsed = 0f;
                _activeHostDrawPending = false;
                StoreSignature(active, in traceEnd);
            }

            _dwellElapsed += Time.deltaTime;
            _activeHostElapsed += Time.deltaTime;

            // Once shown for a stable signature, only two things move it on from here — see
            // AdvanceOrRebloomActiveHost. Re-calling Show for any other reason is exactly what would
            // reposition the pens while the figure is visible, which is the invariant this whole scheme
            // exists to hold.
            if (_shown)
            {
                AdvanceOrRebloomActiveHost(active, spinDegrees, spinSettled, spinning, in traceEnd);
                return;
            }

            if (_dwellElapsed < _config.SightDelaySeconds)
            {
                return;
            }

            // A Laser's figure is its rotated cross — building it from a mid-transition angle draws a
            // cross the beam isn't actually aligned to, which then silently "corrects" on the next
            // rotation and reads as a glitch. ShowActiveHost only actually draws once spinSettled is
            // true, so _shown only latches once the figure is genuinely on screen; while it stays false
            // this keeps retrying every frame at no extra cost, since the rotation dwells for most of
            // every step.
            _shown = ShowActiveHost(active, spinDegrees, spinSettled, in traceEnd);
        }

        // Runs only on a version change (the expensive grid walk), and only decides WHICH hosts are
        // sighted — the signature comparison, sequencing and dwell timer that decide WHEN (and WHAT) to
        // draw live in LateTick, which keeps running every frame regardless of whether this method does.
        private void RefreshSightedHosts()
        {
            CollectSightedHosts();

            // _shownSetSlots/_hasShownSet is a "last actually shown" cache, only ever written by
            // ShowActiveHost once a signature has dwelt long enough to draw — NOT a live mirror of what
            // is currently sighted. A sweep that lands on a DIFFERENT set and back onto this one, too
            // quickly for that other set to ever dwell its own way to a Show, would otherwise leave
            // _shownSetSlots pinned on the original set the whole time: by the time the aim resettles
            // there, the sighted set equals _shownSetSlots again and the detour is invisible to
            // ShowActiveHost's introduce check, so the figure would reappear already formed instead of
            // re-blooming. Sighting a SET the figure isn't actually shown for is itself proof the aim
            // left it, dwell or no dwell — compared as a whole set now, not a single slot, so stepping
            // through an unchanged set via AdvanceSequence (which alters no slot this compares) never
            // trips it, only a genuinely different aim does.
            if (_hasShownSet && !SlotsEqual(_shownSetSlots, _sightedHosts))
            {
                _hasShownSet = false;
            }

            _hasSightedHosts = _sightedHosts.Count > 0;
        }

        // isSettled defaults true for a non-spinning (or unhosted) slot — nothing is ever in flight for it,
        // so it can never register a settled-to-unsettled edge (HasSpinLeftSettledAngle), which correctly
        // keeps it a no-op, exactly as before this existed. spinning is null for that same case, which
        // HoldLoopMayRebloom reads as "no dwell of its own to measure — the hold loop stays its only
        // cadence," exactly as before this existed either.
        private float ResolveSpinDegrees(Vector2Int slot, out bool isSettled, out ISpinningItemVisual spinning)
        {
            spinning = _grid.ViewAt(slot) is IHostsSpinningItem spinHost ? spinHost.SpinningItem : null;
            isSettled = spinning?.IsSettled ?? true;
            return spinning?.AngleDegrees ?? 0f;
        }

        private void HideAndClearSignature()
        {
            _ticker.BeginHide();
            _hasSightedHosts = false;
            _sequenceIndex = 0;
            _hasSignature = false;
            _shown = false;
            _dwellElapsed = 0f;
            _activeHostElapsed = 0f;
            _activeHostDrawPending = false;
            _hasShownSet = false;
            _hasPreviousSpinSettled = false;
            _earlyCycleEndRequested = false;
        }

        // The one place a shown figure's turn can move on, now that both ways of getting here go through
        // the very same fade-and-park cycle. CycleComplete only ever turns true once the ticker has
        // already faded out and parked (see ItemPreviewTicker.Park) — whether that park was the ticker's
        // own scheduled loop end (AdvanceRebloomCycle, once RebloomHoldSeconds elapses) or one requested
        // early below (ItemPreviewTicker.RequestEarlyCycleEnd) — so every branch here only ever acts once
        // the figure is already dark, never repositioning a visible one.
        //
        // Once parked, _activeHostElapsed decides what happens next — the same budget that used to live
        // only in the now-removed ReactToSpinSettle. A natural CycleComplete always arrives well past
        // oneCycleSeconds (a full Draw+Hold+Fade already takes at least that long), so a non-spinning host
        // keeps behaving exactly as before: advance to the next host, or wrap to the only one on a
        // single-host sequence — except that wrap IS a re-bloom in place (the sequence has nowhere else to
        // go), so TurnTimerMayAdvance holds it to the same HoldLoopMayRebloom qualification the spin-forced
        // branch below already answers to, rather than letting the turn timer bypass it. A spin-forced
        // park can arrive early, and that is the one case this now also has to decide: still within its
        // turn, re-show the SAME host — the re-bloom, and it genuinely blooms now, because the ticker
        // parked and dropped _visible in between, unlike calling Show directly on a still-visible figure —
        // or, if its turn already ran long, advance instead of re-blooming again. Gated on
        // RebloomHoldSeconds > 0 to match AdvanceRebloomCycle's own off switch: with looping authored off,
        // a park can only ever be spin-forced (the natural loop never parks at all), and it must keep
        // re-blooming the same host in place forever, exactly as before this existed.
        //
        // HasSpinLeftSettledAngle catches the other half: the active host's spin LEAVING its settled step
        // — beginning a transition toward the next one — is the re-bloom trigger, not arriving at the new
        // one. Triggering on arrival left the figure showing an orientation the laser had already turned
        // away from for the whole transition (LaserItemRotation spends its last TransitionFraction easing
        // to the next step) plus the fade, roughly half a second of a visibly stale figure hanging in the
        // air. Firing on the departure instead starts the fade while the rotation is still turning, so the
        // old figure is on its way out as the orientation changes rather than lingering past it. Rather
        // than redrawing in place directly — which used to mean calling Show on a still-visible figure,
        // silently taking the RefitPens branch instead of actually re-blooming — it only ever asks the
        // ticker to end the current cycle early and waits for CycleComplete above to do the actual redraw
        // once the pens are dark. RequestEarlyCycleEnd is idempotent while its own fade (or the loop's) is
        // already running, so calling it again on a later frame before that fade completes costs nothing
        // extra. Because the fade is shorter than the rotation's own transition, the ticker will typically
        // park mid-transition — CycleComplete above waits out the remainder (!spinSettled) before it will
        // draw, so the redraw always lands on the rotation's own arrival, never ahead of it.
        //
        // A third thing this now has to decide, once the turn isn't spent: whether THIS park is even
        // allowed to redraw in place at all. The hold loop's own timer (AdvanceRebloomCycle, off
        // RebloomHoldSeconds) and a spinning host's own step cadence are two independent clocks racing on
        // the same ticker; see HoldLoopMayRebloom for why a host whose dwell can't hold two full redraws
        // must never take the hold loop's redraw, only the rotation's own falling edge
        // (_earlyCycleEndRequested, set where RequestEarlyCycleEnd is called below). A qualifying spinning
        // host, and every non-spinning one, keeps redrawing on the hold loop exactly as before this
        // existed.
        private void AdvanceOrRebloomActiveHost(
            in ItemPreviewSightedHost active, float spinDegrees, bool spinSettled, ISpinningItemVisual spinning,
            in PredictionTraceEnd traceEnd)
        {
            // Tracked first, unconditionally — every frame this method runs, regardless of CycleComplete
            // or any return below. The rotation's own departure is the only thing that can ever authorise
            // a redraw for a host the hold loop has disqualified (see HoldLoopMayRebloom), and it has to
            // be caught even while the ticker sits parked: a parked host has no other clock running, so an
            // edge missed here is an edge that can never be recovered, stranding the figure dark for good.
            // RequestEarlyCycleEnd is already a no-op while parked or fading (see its own remarks), so
            // calling it on the edge in any state is safe — only the flag set alongside it matters.
            if (HasSpinLeftSettledAngle(spinSettled))
            {
                _ticker.RequestEarlyCycleEnd();
                _earlyCycleEndRequested = true;
            }

            _previousSpinSettled = spinSettled;
            _hasPreviousSpinSettled = true;

            if (!_ticker.CycleComplete)
            {
                return;
            }

            if (!spinSettled)
            {
                // Parked mid-transition: nothing to draw until the rotation itself lands. No work
                // queued, no allocation — just wait for a later frame's spinSettled to read true.
                return;
            }

            if (_activeHostDrawPending)
            {
                // AdvanceSequence already moved onto this host while its rotation was mid-transition and
                // deferred the draw (see ShowHost) — _activeHostElapsed is stale evidence of the
                // PREVIOUS host's turn running out, not this one's. Finish the deferred draw directly
                // instead of falling into the turn-timer decision below, which would misread that stale
                // value as another turn already expired and advance straight past this host unseen.
                if (ShowHost(in active, spinDegrees, spinSettled, in traceEnd, introduce: true))
                {
                    _activeHostDrawPending = false;
                    _activeHostElapsed = 0f;
                }

                return;
            }

            var oneCycleSeconds = _config.BloomDuration + _config.RebloomHoldSeconds;

            // A one-host sequence's "advance" wraps straight back to itself (AdvanceSequence's own mod
            // arithmetic), which makes it indistinguishable from a re-bloom in place — driven purely by
            // this same turn timer, on the hold cadence, with nothing to tell it apart from the qualified
            // re-bloom below except which branch happened to run first. TurnTimerMayAdvance is what stops
            // that wrap from bypassing HoldLoopMayRebloom's dwell qualification the way a genuine advance
            // to a DIFFERENT host must not be held up by any one host's own rotation.
            if (_config.RebloomHoldSeconds > 0f && _activeHostElapsed >= oneCycleSeconds &&
                TurnTimerMayAdvance(_sightedHosts.Count, spinning, oneCycleSeconds))
            {
                AdvanceSequence(in traceEnd);
            }
            else if (HoldLoopMayRebloom(spinning, oneCycleSeconds) || _earlyCycleEndRequested)
            {
                ShowHost(in active, spinDegrees, spinSettled, in traceEnd, introduce: true);
            }

            // Disqualified, and this park wasn't the rotation's own doing: leave the figure dark and
            // unconsumed. CycleComplete stays true (see its own remarks) until either the turn above
            // runs out on a later frame, or the rotation's next falling edge asks again — nothing to
            // queue here in the meantime.
        }

        // True on the FALLING edge of ISpinningItemVisual.IsSettled — settled last frame, not settled this
        // frame — i.e. the rotation just left its dwell and started easing toward the next step. Reads the
        // flag itself rather than inferring "still moving" from a frame-to-frame angle delta: LaserItemRotation
        // eases its transition with Mathf.SmoothStep, whose per-frame delta approaches zero as the
        // transition ends, so a delta-based guess would misread the tail end of an OUTGOING transition as
        // already-settled. IsSettled has no such tail — it is the exact same boolean DrawnAngle branches on
        // (LaserItemRotation.IsDwelling) — so this edge is genuine, not a threshold guess.
        private bool HasSpinLeftSettledAngle(bool spinSettled)
        {
            return _hasPreviousSpinSettled && _previousSpinSettled && !spinSettled;
        }

        // A hold-driven re-bloom only makes sense if the host stays at one angle long enough to hold at
        // least two full redraws — otherwise the hold loop's own cadence (oneCycleSeconds) and the
        // rotation's own dwell/step cadence drift past each other, and a hold-loop re-bloom can land just
        // before the rotation turns, cutting the draw-in short before the rotation's own fade takes over
        // (see the Item README for the concrete cadence numbers that motivated this). oneCycleSeconds
        // omits the ribbon fade that actually ends each cycle, so it slightly understates a full redraw —
        // fine for this heuristic (and it keeps this controller from needing the pen prefab's own
        // lifetime), but do not tighten it into an exact figure; that would change which hosts qualify.
        // Non-spinning hosts (spinning == null) have no dwell of their own to measure against, so the hold
        // loop stays their only cadence, exactly as before this existed. Internal rather than private so
        // it is edit-mode testable directly, mirroring LaserItemRotation.IsDwelling/DwellDuration.
        internal static bool HoldLoopMayRebloom(ISpinningItemVisual spinning, float oneCycleSeconds)
        {
            return spinning == null || spinning.DwellSeconds >= 2f * oneCycleSeconds;
        }

        // Whether the turn timer's advance may actually run once it fires. A sequence of more than one
        // host is a genuine advance to a DIFFERENT host — that has nothing to do with any one host's own
        // rotation, so it always may. A one-host sequence's advance wraps back onto itself
        // (AdvanceSequence's `% _sightedHosts.Count`), which is a re-bloom in every way that matters, so it
        // has to clear the very same dwell qualification the rotation's own re-bloom-in-place branch does
        // — otherwise the turn timer would silently re-bloom a disqualified host the hold-loop check right
        // below it exists specifically to stop. Internal rather than private so this decision is edit-mode
        // testable directly, mirroring HoldLoopMayRebloom.
        internal static bool TurnTimerMayAdvance(int hostCount, ISpinningItemVisual spinning, float oneCycleSeconds)
        {
            return hostCount > 1 || HoldLoopMayRebloom(spinning, oneCycleSeconds);
        }

        // TraceEnd.Kind is exact (a different contact type IS a different aim, however close the geometry);
        // everything else is a Vector2/float that can carry float jitter from a physically-held-still aim,
        // so those compare against SignatureEpsilon(Sq) instead of equality. The active host's own slot is
        // deliberately not compared here — with the set unchanged (the caller only reaches this when
        // setChanged is false), the active index still points at the same slot it did last frame, so
        // comparing it again would say nothing HasSetChanged hasn't already said. Spin is deliberately not
        // compared here either, even though it is as continuous a reading as origin/direction: a Laser's
        // rotation changes it every single frame for a quarter of every step (LaserItemRotation.DrawnAngle's
        // lerp), and tearing the dwell/settled state down on every one of those frames — the old
        // behaviour — is what stuck the sequence on it forever. A settled spin change is its own,
        // non-invalidating re-bloom instead — see HasSpinLeftSettledAngle and
        // AdvanceOrRebloomActiveHost.
        private bool HasActiveSignatureChanged(in ItemPreviewSightedHost active, in PredictionTraceEnd traceEnd)
        {
            return (active.Origin - _signatureOrigin).sqrMagnitude > SignatureEpsilonSq ||
                (active.Direction - _signatureDirection).sqrMagnitude > SignatureEpsilonSq ||
                traceEnd.Kind != _signatureTraceKind ||
                (traceEnd.Normal - _signatureTraceNormal).sqrMagnitude > SignatureEpsilonSq;
        }

        // Whole-set equality against the signature's own slot snapshot — count first (cheap, and the only
        // check most changes need), then an exact per-slot compare in trace order. Order matters as much as
        // membership: two hosts swapping which sequences first is as genuine a change as one appearing or
        // disappearing.
        private bool HasSetChanged()
        {
            return !SlotsEqual(_signatureSlots, _sightedHosts);
        }

        private static bool SlotsEqual(IReadOnlyList<Vector2Int> storedSlots, IReadOnlyList<ItemPreviewSightedHost> hosts)
        {
            if (storedSlots.Count != hosts.Count)
            {
                return false;
            }

            for (var i = 0; i < storedSlots.Count; i++)
            {
                if (storedSlots[i] != hosts[i].Slot)
                {
                    return false;
                }
            }

            return true;
        }

        private static void CopySlots(List<Vector2Int> destination, IReadOnlyList<ItemPreviewSightedHost> hosts)
        {
            destination.Clear();
            for (var i = 0; i < hosts.Count; i++)
            {
                destination.Add(hosts[i].Slot);
            }
        }

        private void StoreSignature(in ItemPreviewSightedHost active, in PredictionTraceEnd traceEnd)
        {
            _hasSignature = true;
            CopySlots(_signatureSlots, _sightedHosts);
            _signatureOrigin = active.Origin;
            _signatureDirection = active.Direction;
            _signatureTraceKind = traceEnd.Kind;
            _signatureTraceNormal = traceEnd.Normal;
        }

        // Returns whether it actually drew — see ShowHost. LateTick only latches _shown once this returns
        // true, so a spinning host that is mid-transition when the dwell elapses simply keeps this
        // returning false (and _shown staying false) until a later frame finds the rotation settled.
        private bool ShowActiveHost(in ItemPreviewSightedHost active, float spinDegrees, bool spinSettled, in PredictionTraceEnd traceEnd)
        {
            // A different SET than the one last actually shown is a genuinely new sequence (or the first
            // one); the same SET means the aim only nudged and settled back on a group of hosts already
            // being telegraphed, so the re-appearance should not re-bloom. Compared as a whole set, not
            // just the active slot — this only ever runs for a freshly (re)started sequence, so the active
            // slot is index 0 by construction, but that alone says nothing about whether the OTHER hosts
            // in the sequence are the same ones as last time.
            var introduce = !_hasShownSet || !SlotsEqual(_shownSetSlots, _sightedHosts);
            if (!ShowHost(in active, spinDegrees, spinSettled, in traceEnd, introduce))
            {
                return false;
            }

            _hasShownSet = true;
            CopySlots(_shownSetSlots, _sightedHosts);

            // The dwell that led here is exactly the time this host was NOT on screen — see
            // _activeHostElapsed's own remarks for why its turn budget starts counting only now, at the
            // actual draw, rather than back when the signature that triggered this dwell was stored.
            _activeHostElapsed = 0f;
            return true;
        }

        // Replays the sequence: the NEXT host in the sighted set — wrapping past the last back to the
        // first — draws in with introduce: true, exactly like the single-host replay this generalizes. A
        // one-element set advances to itself (0 -> 0 mod 1), which is exactly today's single-host replay —
        // the sequence collapses to that case rather than needing a special one. Called from exactly one
        // place now — AdvanceOrRebloomActiveHost's CycleComplete branch — once the ticker has parked,
        // whether that park was its own scheduled loop end or one requested early by a spin settling with
        // its turn already spent (see ItemPreviewTicker.RequestEarlyCycleEnd); either way Park already
        // dropped _visible, so the pens ARE dark and ARE due a fresh draw-in regardless of which host
        // comes next.
        //
        // Updates only the signature's ACTIVE-host geometry (origin/direction/trace end), never
        // _signatureSlots — the set itself hasn't changed, only the sequence's position within it, and
        // routing this through StoreSignature's full reset would make LateTick read this very advance as a
        // changed aim on the very next frame (see HasActiveSignatureChanged). The spin edge tracker
        // (_hasPreviousSpinSettled) is reset by the shared ShowHost helper below on an actual draw, not
        // here directly, so the newly active host's first reading is never mistaken for a falling edge.
        // The index moves unconditionally — the new host becomes active regardless of whether its own
        // rotation happens to be mid-transition right now — but _activeHostElapsed only resets once ShowHost
        // reports it actually drew: the newly active host's turn budget must start at the actual draw, the
        // same rule ShowActiveHost follows, not at the moment it merely became active. A host that can't
        // draw yet (mid-transition) leaves _activeHostDrawPending set so AdvanceOrRebloomActiveHost can
        // finish this draw on a later frame once the rotation settles, instead of re-running the turn-timer
        // decision against a budget that never got to start.
        private void AdvanceSequence(in PredictionTraceEnd traceEnd)
        {
            _sequenceIndex = (_sequenceIndex + 1) % _sightedHosts.Count;
            var active = _sightedHosts[_sequenceIndex];
            var spinDegrees = ResolveSpinDegrees(active.Slot, out var spinSettled, out _);

            _signatureOrigin = active.Origin;
            _signatureDirection = active.Direction;
            _signatureTraceKind = traceEnd.Kind;
            _signatureTraceNormal = traceEnd.Normal;

            if (ShowHost(in active, spinDegrees, spinSettled, in traceEnd, introduce: true))
            {
                _activeHostElapsed = 0f;
            }
            else
            {
                _activeHostDrawPending = true;
            }
        }

        // The one place BuildContext, ItemPreviewTicker.Show and resetting the spin edge tracker
        // (_hasPreviousSpinSettled) happen together — every draw call in this file (a fresh dwell via
        // ShowActiveHost, a sequence advance via AdvanceSequence, and a spin-triggered re-bloom via
        // AdvanceOrRebloomActiveHost's CycleComplete branch) funnels through it, so the three operations
        // can't drift out of lockstep the way three separate copies of the same triple once did. Resetting
        // the tracker on every draw is what stops a host's edge state from ever surviving onto a DIFFERENT
        // active host (see AdvanceSequence and _previousSpinSettled's own remarks): the very next frame's
        // reading, whatever it is, becomes the new baseline rather than being compared against whatever the
        // previous host last reported. _earlyCycleEndRequested is cleared here for the same reason: once
        // this draw starts a fresh cycle, whether THAT cycle's eventual park was rotation-requested has to
        // be decided fresh too, never inherited from whatever the previous cycle (or host) last asked for.
        // Callers are responsible for everything else a particular draw implies (introduce,
        // signature/sequence bookkeeping, _activeHostElapsed) — this only ever does the draw itself.
        //
        // Also the one place the settle guard lives, and returns whether it actually drew. A spinning
        // host's figure is built from its rotation's CURRENT angle (BuildContext -> spinDegrees), so
        // drawing while spinSettled is false would bake in a mid-transition angle the beam isn't actually
        // aligned to — it would then look correct only once the NEXT rotation happens to trigger a
        // redraw, reading as a glitch that silently self-corrects. Every caller funnels through here so
        // this only has to be checked once rather than three times risking drift; each caller holds its
        // own bookkeeping (introduce, signature/sequence state, _activeHostElapsed) back when this
        // returns false, exactly as it already does when this returns true.
        private bool ShowHost(
            in ItemPreviewSightedHost host, float spinDegrees, bool spinSettled, in PredictionTraceEnd traceEnd,
            bool introduce)
        {
            if (!spinSettled)
            {
                // No work queued, no allocation — just wait for a later frame's spinSettled to read true.
                return false;
            }

            var context = BuildContext(in host, spinDegrees, in traceEnd);
            _ticker.Show(host.Preview, in context, introduce);
            _hasPreviousSpinSettled = false;
            _earlyCycleEndRequested = false;
            return true;
        }

        private ItemPreviewContext BuildContext(in ItemPreviewSightedHost active, float spinDegrees, in PredictionTraceEnd traceEnd)
        {
            var colorId = _grid.At(active.Slot) is IHasColor colored ? colored.Color.Value : null;
            return new ItemPreviewContext(
                active.Origin, active.Slot, active.Direction, _traceProvider.Points, colorId, spinDegrees,
                traceEnd, _viewport);
        }

        // Collects every item host the trace crosses, then orders them first-to-last along the line via
        // ItemPreviewSightOrder — the sequence the telegraph steps through, rather than picking a single
        // winner by Centrality the way this used to. Centrality still comes back out of TryScoreHost
        // (TraceHitGeometry always reports it), it just no longer decides anything here: how squarely the
        // line crosses a balloon is a different question from how far along the line that balloon sits,
        // and only the latter orders a sequence.
        private void CollectSightedHosts()
        {
            _sightedHosts.Clear();

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (!TryScoreHost(candidate, out var hit) || !_previewMap.TryGetValue(hit.Item, out var preview))
                    {
                        continue;
                    }

                    _sightedHosts.Add(new ItemPreviewSightedHost(candidate, preview, hit.Origin, hit.Direction));
                }
            }

            ItemPreviewSightOrder.OrderAlongTrace(_sightedHosts, _traceProvider.Points);
        }

        // One slot's candidacy: it must host an item, have live geometry to aim at (an actor mid-despawn or
        // one with no collider authored is not a target), and be crossed by the trace.
        private bool TryScoreHost(Vector2Int slot, out HostHit hit)
        {
            hit = default;

            if (_grid.At(slot) is not IHasItemSlot host || host.Item.Value == ItemType.None)
            {
                return false;
            }

            var view = _grid.ViewAt(slot);
            if (view == null || !view.HasActiveCollider || view.ContactRadius <= 0f)
            {
                return false;
            }

            if (!TraceHitGeometry.TryFindSurfaceHit(
                    _traceProvider.Points, view.ContactCenter, view.ContactRadius, out _,
                    out var centrality, out var direction))
            {
                return false;
            }

            hit = new HostHit(host.Item.Value, view.ContactCenter, direction, centrality);
            return true;
        }

        private readonly struct HostHit
        {
            internal readonly ItemType Item;
            internal readonly Vector2 Origin;
            internal readonly Vector2 Direction;
            internal readonly float Centrality;

            internal HostHit(ItemType item, Vector2 origin, Vector2 direction, float centrality)
            {
                Item = item;
                Origin = origin;
                Direction = direction;
                Centrality = centrality;
            }
        }
    }
}
