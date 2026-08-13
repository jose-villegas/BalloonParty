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
        private readonly SlotGrid _grid;
        private readonly PredictionTraceProvider _traceProvider;
        private readonly ItemPreviewTicker _ticker;
        private readonly ItemPreviewViewport _viewport;
        private readonly IItemPreviewConfig _config;
        private readonly IEnumerable<IItemRangePreview> _previews;

        private Dictionary<ItemType, IItemRangePreview> _previewMap;
        private int _lastVersion = int.MinValue;

        // Dwell state for the currently sighted host, kept separate from _lastVersion's expensive-work
        // gate: the timer below must advance every frame a host stays sighted, including the frames the
        // aim is held perfectly still and the version gate above skips the grid walk entirely.
        private bool _hasSightedHost;
        private bool _shownForSightedHost;
        private Vector2Int _sightedSlot;
        private Vector2 _sightedOrigin;
        private Vector2 _sightedDirection;
        private IItemRangePreview _sightedPreview;
        private float _dwellElapsed;

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
                ResetDwell();
                _ticker.Hide();
                return;
            }

            // Gated on the trace version: a held aim re-walks nothing. The grid walk therefore runs when
            // the AIM moves, not when the board settles — a host drifting under a motionless aim lags by
            // design (@ref plan_item_range_preview open questions). The dwell timer below is NOT gated the
            // same way: it must keep accumulating on the held-still frames this skips, or a perfectly
            // still aim would never satisfy the delay.
            if (_traceProvider.Version != _lastVersion)
            {
                _lastVersion = _traceProvider.Version;
                RefreshSightedHost();
            }

            if (!_hasSightedHost)
            {
                return;
            }

            _dwellElapsed += Time.deltaTime;

            // Once shown, later version changes are handled inside RefreshSightedHost itself (it re-Shows
            // on every refresh once past dwell) — this only gates the FIRST appearance for this host.
            if (_shownForSightedHost || _dwellElapsed < _config.SightDelaySeconds)
            {
                return;
            }

            _shownForSightedHost = true;
            ShowSightedHost();
        }

        // Runs only on a version change (the expensive grid walk), and only decides WHAT is sighted —
        // the dwell timer that gates WHEN it draws lives in LateTick, which keeps running every frame
        // regardless of whether this method does.
        private void RefreshSightedHost()
        {
            if (!TryFindSightedHost(out var slot, out var itemType, out var origin, out var direction) ||
                !_previewMap.TryGetValue(itemType, out var preview))
            {
                ResetDwell();
                _ticker.Hide();
                return;
            }

            // A different host restarts the delay from zero — sweeping past several items shows nothing
            // until the aim settles on one, which is the whole point of the dwell.
            if (!_hasSightedHost || slot != _sightedSlot)
            {
                _hasSightedHost = true;
                _sightedSlot = slot;
                _dwellElapsed = 0f;
                _shownForSightedHost = false;
                _ticker.Hide();
            }

            _sightedOrigin = origin;
            _sightedDirection = direction;
            _sightedPreview = preview;

            // The same host, already past its dwell: keep tracking the aim exactly as before the delay
            // existed — only the first appearance for a host is gated, never a later refresh.
            if (_shownForSightedHost)
            {
                ShowSightedHost();
            }
        }

        private void ShowSightedHost()
        {
            var colorId = _grid.At(_sightedSlot) is IHasColor colored ? colored.Color.Value : null;
            var spinDegrees = _grid.ViewAt(_sightedSlot) is IHostsSpinningItem spinHost
                ? spinHost.SpinningItem?.AngleDegrees ?? 0f
                : 0f;
            var context = new ItemPreviewContext(
                _sightedOrigin, _sightedSlot, _sightedDirection, _traceProvider.Points, colorId, spinDegrees,
                _traceProvider.End, _viewport);

            _ticker.Show(_sightedPreview, in context);
        }

        private void ResetDwell()
        {
            _hasSightedHost = false;
            _shownForSightedHost = false;
            _dwellElapsed = 0f;
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
