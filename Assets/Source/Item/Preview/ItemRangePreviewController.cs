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
    ///     Decides which item host the aim is sighted on, and drives the one visible range telegraph for it.
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
    ///         single host owns the board-level figure — and shares only the underlying
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

        private Dictionary<ItemType, IItemRangePreview> _previewMap;
        private int _lastVersion = int.MinValue;

        private bool _hasSightedHost;
        private Vector2Int _sightedSlot;
        private Vector2 _sightedOrigin;
        private Vector2 _sightedDirection;
        private IItemRangePreview _sightedPreview;

        // The figure's inputs as of the last frame the signature was checked, compared against this
        // frame's to decide whether the aim actually moved. NOT PredictionTraceProvider.Version — that
        // increments every Tick while aiming (ThrowerController.UpdatePredictionTrace calls SetTrace
        // unconditionally) regardless of whether the aim moved, so it can't stand in for this.
        private bool _hasSignature;
        private Vector2Int _signatureSlot;
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

        // The slot of the host the figure was last actually shown for, so ShowSightedHost can tell a
        // re-settle on the SAME host (an aim nudge that lands back on what was already being telegraphed)
        // apart from a genuinely new one — see ItemPreviewTicker.Show's introduce parameter. Cleared
        // whenever the telegraph goes away entirely (trace inactive, or no host sighted), so looking away
        // and back at the same item introduces again rather than reappearing in place.
        private bool _hasShownHost;
        private Vector2Int _shownSlot;

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
                RefreshSightedHost();
            }

            if (!_hasSightedHost)
            {
                HideAndClearSignature();
                return;
            }

            var spinDegrees = ResolveSpinDegrees(_sightedSlot);
            var traceEnd = _traceProvider.End;

            // A changed signature (including a different host) hides gracefully and restarts the dwell —
            // but falls through to the accumulate-and-check below instead of returning, so a
            // SightDelaySeconds of 0 still shows on this same frame rather than costing one.
            if (!_hasSignature ||
                HasSignatureChanged(_sightedSlot, _sightedOrigin, _sightedDirection, spinDegrees, in traceEnd))
            {
                _ticker.BeginHide();
                _shown = false;
                _dwellElapsed = 0f;
                StoreSignature(_sightedSlot, _sightedOrigin, _sightedDirection, spinDegrees, in traceEnd);
            }

            _dwellElapsed += Time.deltaTime;

            // Once shown for a stable signature, do nothing on later stable frames: re-calling Show is
            // exactly what would reposition the pens while the figure is visible, which is the invariant
            // this whole scheme exists to hold.
            if (_shown || _dwellElapsed < _config.SightDelaySeconds)
            {
                return;
            }

            _shown = true;
            ShowSightedHost(spinDegrees, in traceEnd);
        }

        // Runs only on a version change (the expensive grid walk), and only decides WHAT is sighted —
        // the signature comparison and dwell timer that decide WHEN (and whether) to draw it live in
        // LateTick, which keeps running every frame regardless of whether this method does.
        private void RefreshSightedHost()
        {
            if (!TryFindSightedHost(out var slot, out var itemType, out var origin, out var direction) ||
                !_previewMap.TryGetValue(itemType, out var preview))
            {
                _hasSightedHost = false;
                return;
            }

            _hasSightedHost = true;
            _sightedSlot = slot;
            _sightedOrigin = origin;
            _sightedDirection = direction;
            _sightedPreview = preview;
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
            _hasSightedHost = false;
            _hasSignature = false;
            _shown = false;
            _dwellElapsed = 0f;
            _hasShownHost = false;
        }

        // Slot and TraceEnd.Kind are exact (a different slot or contact type IS a different aim, however
        // close the geometry); everything else is a Vector2/float that can carry float jitter from a
        // physically-held-still aim, so those compare against SignatureEpsilon(Sq) instead of equality.
        private bool HasSignatureChanged(
            Vector2Int slot, Vector2 origin, Vector2 direction, float spinDegrees, in PredictionTraceEnd traceEnd)
        {
            return slot != _signatureSlot ||
                (origin - _signatureOrigin).sqrMagnitude > SignatureEpsilonSq ||
                (direction - _signatureDirection).sqrMagnitude > SignatureEpsilonSq ||
                Mathf.Abs(spinDegrees - _signatureSpinDegrees) > SignatureEpsilon ||
                traceEnd.Kind != _signatureTraceKind ||
                (traceEnd.Normal - _signatureTraceNormal).sqrMagnitude > SignatureEpsilonSq;
        }

        private void StoreSignature(
            Vector2Int slot, Vector2 origin, Vector2 direction, float spinDegrees, in PredictionTraceEnd traceEnd)
        {
            _hasSignature = true;
            _signatureSlot = slot;
            _signatureOrigin = origin;
            _signatureDirection = direction;
            _signatureSpinDegrees = spinDegrees;
            _signatureTraceKind = traceEnd.Kind;
            _signatureTraceNormal = traceEnd.Normal;
        }

        private void ShowSightedHost(float spinDegrees, in PredictionTraceEnd traceEnd)
        {
            var colorId = _grid.At(_sightedSlot) is IHasColor colored ? colored.Color.Value : null;
            var context = new ItemPreviewContext(
                _sightedOrigin, _sightedSlot, _sightedDirection, _traceProvider.Points, colorId, spinDegrees,
                traceEnd, _viewport);

            // A different slot than the one last actually shown is a genuinely new figure (or the first
            // one); the same slot means the aim only nudged and settled back on a host already being
            // telegraphed, so the re-appearance should not re-bloom.
            var introduce = !_hasShownHost || _sightedSlot != _shownSlot;
            _ticker.Show(_sightedPreview, in context, introduce);
            _hasShownHost = true;
            _shownSlot = _sightedSlot;
        }

        // The most centrally-struck item host wins, mirroring TraceHitGeometry's own scoring: an aim
        // threading two item balloons telegraphs the one it actually points at, not whichever the grid walk
        // reached first.
        private bool TryFindSightedHost(
            out Vector2Int slot, out ItemType itemType, out Vector2 origin, out Vector2 direction)
        {
            slot = default;
            itemType = ItemType.None;
            origin = default;
            direction = default;
            var bestCentrality = -1f;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var candidate = new Vector2Int(col, row);
                    if (!TryScoreHost(candidate, out var hit) || hit.Centrality <= bestCentrality)
                    {
                        continue;
                    }

                    bestCentrality = hit.Centrality;
                    slot = candidate;
                    itemType = hit.Item;
                    origin = hit.Origin;
                    direction = hit.Direction;
                }
            }

            return bestCentrality >= 0f;
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
