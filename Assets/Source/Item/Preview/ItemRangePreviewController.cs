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
        // _signatureSlots (above) is the SET half of the signature; these five are the active host's own
        // continuous geometry, updated both when the set changes (StoreSignature) and when the sequence
        // advances to a new active host (AdvanceSequence) — never when neither happens, which is what lets
        // a drifting balloon still invalidate a held signature without every advance doing the same.
        private bool _hasSignature;
        private Vector2 _signatureOrigin;
        private Vector2 _signatureDirection;
        private float _signatureSpinDegrees;
        private PredictionTraceEndKind _signatureTraceKind;
        private Vector2 _signatureTraceNormal;

        // Whether the figure for the current (stable) signature has already been shown — gates Show to
        // once per signature, since calling it again is exactly what would reposition a visible figure's
        // pens.
        private bool _shown;
        private float _dwellElapsed;

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
            var setChanged = !_hasSignature || HasSetChanged();
            _sequenceIndex = setChanged ? 0 : _sequenceIndex;

            var active = _sightedHosts[_sequenceIndex];
            var spinDegrees = ResolveSpinDegrees(active.Slot);
            var traceEnd = _traceProvider.End;

            // A changed signature (a different set, or the active host's own geometry drifting) hides
            // gracefully and restarts the dwell — but falls through to the accumulate-and-check below
            // instead of returning, so a SightDelaySeconds of 0 still shows on this same frame rather than
            // costing one.
            if (setChanged || HasActiveSignatureChanged(active, spinDegrees, in traceEnd))
            {
                _ticker.BeginHide();
                _shown = false;
                _dwellElapsed = 0f;
                StoreSignature(active, spinDegrees, in traceEnd);
            }

            _dwellElapsed += Time.deltaTime;

            // Once shown for a stable signature, do nothing further on later stable frames EXCEPT react
            // to the ticker's own loop completing — re-calling Show for any other reason is exactly what
            // would reposition the pens while the figure is visible, which is the invariant this whole
            // scheme exists to hold. CycleComplete only ever turns true once the ticker has already faded
            // out and parked (see ItemPreviewTicker.Park), so advancing here never violates it: the figure
            // isn't visible any more at the instant this fires, it's about to become visible again — for
            // whichever host comes next in the sequence.
            if (_shown)
            {
                if (_ticker.CycleComplete)
                {
                    AdvanceSequence(in traceEnd);
                }

                return;
            }

            if (_dwellElapsed < _config.SightDelaySeconds)
            {
                return;
            }

            _shown = true;
            ShowActiveHost(active, spinDegrees, in traceEnd);
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

        private float ResolveSpinDegrees(Vector2Int slot)
        {
            return _grid.ViewAt(slot) is IHostsSpinningItem spinHost
                ? spinHost.SpinningItem?.AngleDegrees ?? 0f
                : 0f;
        }

        private void HideAndClearSignature()
        {
            _ticker.BeginHide();
            _hasSightedHosts = false;
            _sequenceIndex = 0;
            _hasSignature = false;
            _shown = false;
            _dwellElapsed = 0f;
            _hasShownSet = false;
        }

        // TraceEnd.Kind is exact (a different contact type IS a different aim, however close the geometry);
        // everything else is a Vector2/float that can carry float jitter from a physically-held-still aim,
        // so those compare against SignatureEpsilon(Sq) instead of equality. The active host's own slot is
        // deliberately not compared here — with the set unchanged (the caller only reaches this when
        // setChanged is false), the active index still points at the same slot it did last frame, so
        // comparing it again would say nothing HasSetChanged hasn't already said.
        private bool HasActiveSignatureChanged(
            in ItemPreviewSightedHost active, float spinDegrees, in PredictionTraceEnd traceEnd)
        {
            return (active.Origin - _signatureOrigin).sqrMagnitude > SignatureEpsilonSq ||
                (active.Direction - _signatureDirection).sqrMagnitude > SignatureEpsilonSq ||
                Mathf.Abs(spinDegrees - _signatureSpinDegrees) > SignatureEpsilon ||
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

        private void StoreSignature(in ItemPreviewSightedHost active, float spinDegrees, in PredictionTraceEnd traceEnd)
        {
            _hasSignature = true;
            CopySlots(_signatureSlots, _sightedHosts);
            _signatureOrigin = active.Origin;
            _signatureDirection = active.Direction;
            _signatureSpinDegrees = spinDegrees;
            _signatureTraceKind = traceEnd.Kind;
            _signatureTraceNormal = traceEnd.Normal;
        }

        private void ShowActiveHost(in ItemPreviewSightedHost active, float spinDegrees, in PredictionTraceEnd traceEnd)
        {
            var context = BuildContext(in active, spinDegrees, in traceEnd);

            // A different SET than the one last actually shown is a genuinely new sequence (or the first
            // one); the same SET means the aim only nudged and settled back on a group of hosts already
            // being telegraphed, so the re-appearance should not re-bloom. Compared as a whole set, not
            // just the active slot — this only ever runs for a freshly (re)started sequence, so the active
            // slot is index 0 by construction, but that alone says nothing about whether the OTHER hosts
            // in the sequence are the same ones as last time.
            var introduce = !_hasShownSet || !SlotsEqual(_shownSetSlots, _sightedHosts);
            _ticker.Show(active.Preview, in context, introduce);
            _hasShownSet = true;
            CopySlots(_shownSetSlots, _sightedHosts);
        }

        // Replays the sequence in response to ItemPreviewTicker.CycleComplete: the ticker's own loop just
        // parked, so this advances to the NEXT host in the sighted set — wrapping past the last back to
        // the first — and draws it in. Always introduce: true, exactly like the single-host replay this
        // generalizes: Park already dropped _visible, so the pens ARE dark and ARE due a fresh draw-in
        // regardless of which host comes next. A one-element set advances to itself (0 -> 0 mod 1), which
        // is exactly today's single-host replay — the sequence collapses to that case rather than needing
        // a special one.
        //
        // Updates only the signature's ACTIVE-host geometry (origin/direction/spin/trace end), never
        // _signatureSlots — the set itself hasn't changed, only the sequence's position within it, and
        // routing this through StoreSignature's full reset would make LateTick read this very advance as a
        // changed aim on the very next frame (see HasActiveSignatureChanged).
        private void AdvanceSequence(in PredictionTraceEnd traceEnd)
        {
            _sequenceIndex = (_sequenceIndex + 1) % _sightedHosts.Count;
            var active = _sightedHosts[_sequenceIndex];
            var spinDegrees = ResolveSpinDegrees(active.Slot);

            _signatureOrigin = active.Origin;
            _signatureDirection = active.Direction;
            _signatureSpinDegrees = spinDegrees;
            _signatureTraceKind = traceEnd.Kind;
            _signatureTraceNormal = traceEnd.Normal;

            var context = BuildContext(in active, spinDegrees, in traceEnd);
            _ticker.Show(active.Preview, in context, introduce: true);
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
