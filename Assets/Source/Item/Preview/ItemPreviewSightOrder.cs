using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     One host the aim's prediction line currently crosses — enough to draw its figure (mirrors
    ///     <c>ItemRangePreviewController.HostHit</c>) plus its own position along the trace
    ///     (<see cref="TraceOffset" />), the key <see cref="ItemPreviewSightOrder" /> sequences several
    ///     sighted hosts by.
    /// </summary>
    internal struct ItemPreviewSightedHost
    {
        internal readonly Vector2Int Slot;
        internal readonly IItemRangePreview Preview;
        internal readonly Vector2 Origin;
        internal readonly Vector2 Direction;

        // Arc-length offset along the trace polyline, filled in by ItemPreviewSightOrder once every host
        // in the set is known — a single host's own offset means nothing until it is measured against the
        // others sighted on the same line.
        internal float TraceOffset;

        internal ItemPreviewSightedHost(Vector2Int slot, IItemRangePreview preview, Vector2 origin, Vector2 direction)
        {
            Slot = slot;
            Preview = preview;
            Origin = origin;
            Direction = direction;
            TraceOffset = 0f;
        }
    }

    /// <summary>
    ///     Orders a set of sighted item hosts first-to-last along the trace they were found on — the
    ///     sequence <see cref="ItemRangePreviewController" /> steps a multi-host telegraph through.
    /// </summary>
    /// <remarks>
    ///     Pure geometry, factored out of the controller so the interesting case — a bent, multi-segment
    ///     trace, where a host spatially near the aim's own origin can still sit late in true arc-length
    ///     order — is edit-mode testable without the controller's grid/pool machinery, mirroring
    ///     <see cref="ItemPreviewEntry" />'s own separation from <see cref="ItemPreviewTicker" />. Orders
    ///     by each host's own projection onto the trace
    ///     (<see cref="ItemPreviewEntry.TryFindNearestPointOnPolyline" />), never by <c>Centrality</c> —
    ///     how squarely the line crosses a balloon says nothing about how far along the line that balloon
    ///     sits, which is the only thing a sequence cares about.
    /// </remarks>
    internal static class ItemPreviewSightOrder
    {
        private static readonly TraceOffsetComparer Comparer = new();

        internal static void OrderAlongTrace(List<ItemPreviewSightedHost> hosts, IReadOnlyList<Vector3> tracePoints)
        {
            for (var i = 0; i < hosts.Count; i++)
            {
                var host = hosts[i];
                ItemPreviewEntry.TryFindNearestPointOnPolyline(
                    tracePoints, new Vector3(host.Origin.x, host.Origin.y, 0f), out _, out host.TraceOffset);
                hosts[i] = host;
            }

            hosts.Sort(Comparer);
        }

        // A cached singleton rather than a Comparison<T> delegate built at the call site, so the grid walk
        // this feeds — gated on trace version, which skips whenever the published trace is unchanged —
        // never allocates a comparer or delegate of its own on the frames it does run.
        private sealed class TraceOffsetComparer : IComparer<ItemPreviewSightedHost>
        {
            public int Compare(ItemPreviewSightedHost a, ItemPreviewSightedHost b)
            {
                return a.TraceOffset.CompareTo(b.TraceOffset);
            }
        }
    }
}
