using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Everything about one aim-crossing that a range figure depends on. Only per-crossing data lives
    ///     here — the services a preview needs (config, grid, walls) are constructor-injected into the
    ///     preview itself, since they never change between crossings.
    /// </summary>
    internal readonly struct ItemPreviewContext
    {
        /// <summary>The host's contact centre — where its figure is anchored and where pens launch from.</summary>
        public readonly Vector2 Origin;

        public readonly Vector2Int Slot;

        /// <summary>Travel direction of the trace leg that crosses the host — the shot's heading on arrival.</summary>
        public readonly Vector2 AimDirection;

        /// <summary>
        ///     The live aim polyline. Figures anchored to the shot rather than the board read it — Shield
        ///     marks its end, Snipe runs its corridor along it.
        /// </summary>
        public readonly IReadOnlyList<Vector3> TracePoints;

        public readonly string HostColorId;

        public ItemPreviewContext(
            Vector2 origin, Vector2Int slot, Vector2 aimDirection, IReadOnlyList<Vector3> tracePoints,
            string hostColorId)
        {
            Origin = origin;
            Slot = slot;
            AimDirection = aimDirection;
            TracePoints = tracePoints;
            HostColorId = hostColorId;
        }
    }
}
