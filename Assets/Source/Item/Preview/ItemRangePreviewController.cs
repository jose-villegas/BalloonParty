using System;
using System.Collections.Generic;
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
        private readonly IEnumerable<IItemRangePreview> _previews;

        private Dictionary<ItemType, IItemRangePreview> _previewMap;
        private int _lastVersion = int.MinValue;

        internal ItemRangePreviewController(
            SlotGrid grid,
            PredictionTraceProvider traceProvider,
            ItemPreviewTicker ticker,
            IEnumerable<IItemRangePreview> previews)
        {
            _grid = grid;
            _traceProvider = traceProvider;
            _ticker = ticker;
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
            if (!_traceProvider.IsActive || _traceProvider.Points.Count < 2)
            {
                _lastVersion = int.MinValue;
                _ticker.Hide();
                return;
            }

            // Gated on the trace version: a held aim re-walks nothing. The figure therefore refreshes when
            // the AIM moves, not when the board settles — a host drifting under a motionless aim lags by
            // design (@ref plan_item_range_preview open questions).
            if (_traceProvider.Version == _lastVersion)
            {
                return;
            }

            _lastVersion = _traceProvider.Version;

            if (!TryFindSightedHost(out var slot, out var itemType, out var origin, out var direction))
            {
                _ticker.Hide();
                return;
            }

            if (!_previewMap.TryGetValue(itemType, out var preview))
            {
                _ticker.Hide();
                return;
            }

            var colorId = _grid.At(slot) is IHasColor colored ? colored.Color.Value : null;
            var spinDegrees = _grid.ViewAt(slot) is IHostsSpinningItem spinHost
                ? spinHost.SpinningItem?.AngleDegrees ?? 0f
                : 0f;
            var context = new ItemPreviewContext(
                origin, slot, direction, _traceProvider.Points, colorId, spinDegrees, _traceProvider.End);

            _ticker.Show(preview, in context);
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
