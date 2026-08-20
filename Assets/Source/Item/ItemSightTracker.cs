using System.Collections.Generic;
using System.Text;
using BalloonParty.Configuration.Items;
using BalloonParty.Item.Preview;
using BalloonParty.Prediction;
using BalloonParty.Shared.Diagnostics;
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

        private static readonly TraceOffsetComparer OffsetComparer = new();

        private readonly SlotGrid _grid;
        private readonly PredictionTraceProvider _traceProvider;

        private readonly List<SightedItem> _sighted = new();
        private readonly List<ItemSightHit> _rawScratch = new();
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

        // Raw candidacy (active collider, non-zero contact radius, crossed by the trace) is
        // ItemSightQuery's job — the single walk shared with ItemRangePreviewController's own sighted
        // set, so the two can't independently drift on what counts as "crossed" the way they once did.
        // Deliberately no centrality threshold here, matching ItemSightQuery's own raw output: a shallow
        // graze along the very edge of an item's contact circle is still a real crossing, and both the
        // preview telegraph and PredictionSightProbe's continuous ramp reactions (SightScalePulseReaction
        // etc., which read raw Sight, not a latch) already treat it as sighted. An earlier version of
        // this method added its own enter/exit hysteresis on top, meant to guard against a settling
        // balloon chattering in and out right at that edge — but ItemSightSoundRouter's own settle gate
        // (SightSoundSettleSeconds) already absorbs that kind of flicker upstream of anything this method
        // decides, so the extra threshold only ever cost precision: a graze the other two systems clearly
        // sighted was silently missing from here, undercounting a real chain. This method's own job is
        // just trace ordering on top of ItemSightQuery's raw walk.
        private void CollectSighted()
        {
            ItemSightQuery.CollectSightedHosts(_grid, _traceProvider.Points, _rawScratch);

            _sighted.Clear();

            for (var i = 0; i < _rawScratch.Count; i++)
            {
                var candidate = _rawScratch[i];
                ItemPreviewEntry.TryFindNearestPointOnPolyline(
                    _traceProvider.Points, candidate.Position, out _, out var traceOffset);
                _sighted.Add(new SightedItem(candidate.Slot, candidate.Position, traceOffset, candidate.Item));
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

        private sealed class TraceOffsetComparer : IComparer<SightedItem>
        {
            public int Compare(SightedItem a, SightedItem b) => a.TraceOffset.CompareTo(b.TraceOffset);
        }
    }
}
