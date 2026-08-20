using System.Collections.Generic;
using System.Text;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Preview;
using BalloonParty.Prediction;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Tracks which item-hosting balloons the aim's prediction line currently crosses, ordered
    ///     first-to-last along it — the single place that answers "what is sighted right now" so a
    ///     consumer (today, <c>Audio/Routing/ItemSightSoundRouter</c>) reads one shared, already-walked
    ///     answer instead of re-running its own grid walk. Logs to the console every time the sighted
    ///     structure itself changes (see <see cref="LogIfChanged" />), purely as a debugging aid for
    ///     watching what the aim is actually crossing without guessing from a downstream effect.
    /// </summary>
    internal sealed class ItemSightTracker : ILateTickable
    {
        // One item host the aim's trace currently crosses, along with its own arc-length position on
        // the trace — the key SightedItem is ordered by (see the comparer below).
        internal readonly struct SightedItem
        {
            internal readonly Vector2Int Slot;
            internal readonly Vector3 Position;
            internal readonly float TraceOffset;
            internal readonly ItemType Item;

            internal SightedItem(Vector2Int slot, Vector3 position, float traceOffset, ItemType item)
            {
                Slot = slot;
                Position = position;
                TraceOffset = traceOffset;
                Item = item;
            }
        }

        // Enter/exit hysteresis on centrality, mirroring PredictionSightProbe's own enter/exit
        // thresholds — a raw "does the trace touch this circle at all" test has zero buffer against a
        // graze right at the edge, and a balloon still drifting into its slot moves just enough
        // between grid walks to cross that razor-thin boundary back and forth, chattering the slot in
        // and out of the sighted set. Once a slot enters (centrality >= Enter), it takes a bigger drop
        // (centrality < Exit) to leave again, the same asymmetric gap the probe uses to keep a graze
        // from chattering its own enter/exit edge.
        private const float SightEnterCentrality = 0.2f;
        private const float SightExitCentrality = 0.05f;

        private static readonly TraceOffsetComparer OffsetComparer = new();

        private readonly SlotGrid _grid;
        private readonly PredictionTraceProvider _traceProvider;

        private readonly List<SightedItem> _sighted = new();
        private readonly List<Vector2Int> _previousRawSlots = new();
        private readonly List<Vector2Int> _lastLoggedSlots = new();
        private readonly StringBuilder _logScratch = new();

        private int _lastVersion = int.MinValue;

        internal IReadOnlyList<SightedItem> Sighted => _sighted;

        [Inject]
        public ItemSightTracker(SlotGrid grid, PredictionTraceProvider traceProvider)
        {
            _grid = grid;
            _traceProvider = traceProvider;
        }

        public void LateTick()
        {
            if (_traceProvider.Version == _lastVersion)
            {
                return;
            }

            _lastVersion = _traceProvider.Version;

            if (_traceProvider.IsActive && _traceProvider.Points.Count >= 2)
            {
                CollectSighted();
            }
            else
            {
                _sighted.Clear();
            }

            LogIfChanged();
        }

        // Whether `current` (already trace-ordered) is the exact same chain as `slots` — same hosts,
        // in the same order. Order-sensitive on purpose: a chain with the same START but a new item
        // further along the path is a DIFFERENT chain even though its first entry matches. Pure so the
        // equality rule itself is verifiable without any grid/trace setup.
        internal static bool ChainMatches(IReadOnlyList<SightedItem> current, IReadOnlyList<Vector2Int> slots)
        {
            if (current.Count != slots.Count)
            {
                return false;
            }

            for (var i = 0; i < current.Count; i++)
            {
                if (current[i].Slot != slots[i])
                {
                    return false;
                }
            }

            return true;
        }

        // Mirrors ItemRangePreviewController.CollectSightedHosts/TryScoreHost — an item host qualifies
        // by the same geometry (active collider, non-zero contact radius, crossed by the trace) — but
        // answers only "is this slot sighted right now", not which preview it should show, so it never
        // touches the preview map. Ordered first-to-last along the trace (the same projection
        // ItemPreviewSightOrder uses). `_previousRawSlots` (this walk's sighted set BEFORE it's
        // overwritten) is what the hysteresis check below reads.
        private void CollectSighted()
        {
            _previousRawSlots.Clear();
            for (var i = 0; i < _sighted.Count; i++)
            {
                _previousRawSlots.Add(_sighted[i].Slot);
            }

            _sighted.Clear();

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (_grid.At(slot) is not IHasItemSlot host || host.Item.Value == ItemType.None)
                    {
                        continue;
                    }

                    var view = _grid.ViewAt(slot);
                    if (view == null || !view.HasActiveCollider || view.ContactRadius <= 0f)
                    {
                        continue;
                    }

                    if (!TraceHitGeometry.TryFindSurfaceHit(
                            _traceProvider.Points, view.ContactCenter, view.ContactRadius, out _,
                            out var centrality, out _))
                    {
                        continue;
                    }

                    var wasSighted = ContainsSlot(_previousRawSlots, slot);
                    if (centrality < (wasSighted ? SightExitCentrality : SightEnterCentrality))
                    {
                        continue;
                    }

                    ItemPreviewEntry.TryFindNearestPointOnPolyline(
                        _traceProvider.Points, view.ContactCenter, out _, out var traceOffset);
                    _sighted.Add(new SightedItem(slot, view.ContactCenter, traceOffset, host.Item.Value));
                }
            }

            _sighted.Sort(OffsetComparer);
        }

        // Debug-only visibility into what the grid walk just found — strips from release builds
        // (Log.Info), so this costs nothing there. Only logs when the STRUCTURE actually changed
        // (slot identity and order), not on every version-gated walk that happens to reconfirm the
        // same chain, so a held aim doesn't spam the console once per frame.
        private void LogIfChanged()
        {
            if (ChainMatches(_sighted, _lastLoggedSlots))
            {
                return;
            }

            _lastLoggedSlots.Clear();
            _logScratch.Clear();
            _logScratch.Append("Sighted (").Append(_sighted.Count).Append("): ");

            for (var i = 0; i < _sighted.Count; i++)
            {
                if (i > 0)
                {
                    _logScratch.Append(", ");
                }

                _logScratch.Append(_sighted[i].Item).Append('@').Append(_sighted[i].Slot);
                _lastLoggedSlots.Add(_sighted[i].Slot);
            }

            if (_sighted.Count == 0)
            {
                _logScratch.Append("(none)");
            }

            Log.Info("ItemSight", _logScratch.ToString());
        }

        private static bool ContainsSlot(IReadOnlyList<Vector2Int> slots, Vector2Int slot)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] == slot)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class TraceOffsetComparer : IComparer<SightedItem>
        {
            public int Compare(SightedItem a, SightedItem b) => a.TraceOffset.CompareTo(b.TraceOffset);
        }
    }
}
