using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Prediction;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>One item host the aim's prediction trace crosses this frame, and its raw hit geometry.</summary>
    internal readonly struct ItemSightHit
    {
        internal readonly Vector2Int Slot;
        internal readonly ItemType Item;
        internal readonly Vector3 Position;
        internal readonly Vector2 Direction;
        internal readonly float Centrality;

        internal ItemSightHit(Vector2Int slot, ItemType item, Vector3 position, Vector2 direction, float centrality)
        {
            Slot = slot;
            Item = item;
            Position = position;
            Direction = direction;
            Centrality = centrality;
        }
    }

    /// <summary>
    ///     The single place that scores a slot (or the whole board) against the aim's prediction trace for
    ///     item-hosting purposes — pure geometry, no ordering or membership policy of its own. Every
    ///     consumer that needs "is this item host sighted, and where" (today <c>ItemRangePreviewController</c>
    ///     and <c>ItemSightTracker</c>) reads through here instead of re-deriving the same qualifying checks
    ///     and <see cref="TraceHitGeometry" /> call independently, which is what let the two drift apart
    ///     before this existed (the preview walk has no enter/exit hysteresis; <c>ItemSightTracker</c>
    ///     does — see its own remarks). Ordering (by trace offset) and membership policy (hysteresis,
    ///     preview-registration filtering) stay each consumer's own concern, layered on top of the raw hits
    ///     this returns.
    /// </summary>
    internal static class ItemSightQuery
    {
        /// <summary>
        ///     One slot's candidacy: it must host an item, have live geometry to aim at (an actor
        ///     mid-despawn or one with no collider authored is not a target), and be crossed by the trace.
        /// </summary>
        internal static bool TryScoreHost(SlotGrid grid, Vector2Int slot, IReadOnlyList<Vector3> tracePoints, out ItemSightHit hit)
        {
            hit = default;

            if (grid.At(slot) is not IHasItemSlot host || host.Item.Value == ItemType.None)
            {
                return false;
            }

            var view = grid.ViewAt(slot);
            if (view == null || !view.HasActiveCollider || view.ContactRadius <= 0f)
            {
                return false;
            }

            if (!TraceHitGeometry.TryFindSurfaceHit(
                    tracePoints, view.ContactCenter, view.ContactRadius, out _, out var centrality, out var direction))
            {
                return false;
            }

            hit = new ItemSightHit(slot, host.Item.Value, view.ContactCenter, direction, centrality);
            return true;
        }

        /// <summary>
        ///     Every item host the trace currently crosses, in grid-walk order (not trace order — ordering
        ///     is each consumer's own concern, since not every consumer wants one: <c>ItemSightTracker</c>
        ///     orders after applying hysteresis, so ordering the raw walk first would be wasted work on
        ///     hits it's about to discard). Clears and reuses <paramref name="results" /> rather than
        ///     allocating, since this runs on every trace-version change.
        /// </summary>
        internal static void CollectSightedHosts(SlotGrid grid, IReadOnlyList<Vector3> tracePoints, List<ItemSightHit> results)
        {
            results.Clear();

            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (TryScoreHost(grid, new Vector2Int(col, row), tracePoints, out var hit))
                    {
                        results.Add(hit);
                    }
                }
            }
        }
    }
}
