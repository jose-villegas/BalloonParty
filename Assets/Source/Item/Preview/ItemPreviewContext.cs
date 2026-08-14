using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Prediction;
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

        /// <summary>
        ///     The hosted item visual's current Z rotation, or 0 when it doesn't spin. Only the Laser's
        ///     icon spins, and its cross is cast along that rotation — drawn axis-aligned while the icon
        ///     visibly turns, the telegraph would point at balloons the beam misses.
        /// </summary>
        /// <remarks>
        ///     This is the angle NOW, not at contact. Live, the cross is cast at
        ///     <c>ItemSpinDegrees + rate * tHit</c>, and while aiming there is no <c>tHit</c> to
        ///     extrapolate to — so the drawn cross is honest about the item's current pose and drifts from
        ///     the one the shot will actually meet (@ref plan_item_range_preview open questions).
        /// </remarks>
        public readonly float ItemSpinDegrees;

        /// <summary>
        ///     What the aim line ran into at its end, and that contact's surface normal — solved by the
        ///     trace calculator, so a figure drawing the bounce leaving it doesn't derive a second answer.
        /// </summary>
        public readonly PredictionTraceEnd TraceEnd;

        /// <summary>
        ///     The shared visible bounds a figure can clip itself to (see <see cref="LaserRangePreview" />),
        ///     null when no viewport is available — test call sites that don't care about clipping default
        ///     to null rather than having to construct one.
        /// </summary>
        public readonly ItemPreviewViewport Viewport;

        /// <summary>
        ///     The host's own balloon type — lets a figure that needs the host's true size (the approach
        ///     loop's circle, see <see cref="ItemPreviewTicker" />) read it off <c>BalloonContactRadii</c>
        ///     rather than guessing. Defaults to the enum's first member for call sites (mostly tests) that
        ///     don't care, same as every other optional trailing param here.
        /// </summary>
        public readonly BalloonType HostBalloonType;

        public ItemPreviewContext(
            Vector2 origin, Vector2Int slot, Vector2 aimDirection, IReadOnlyList<Vector3> tracePoints,
            string hostColorId, float itemSpinDegrees, PredictionTraceEnd traceEnd = default,
            ItemPreviewViewport viewport = null, BalloonType hostBalloonType = default)
        {
            Origin = origin;
            Slot = slot;
            AimDirection = aimDirection;
            TracePoints = tracePoints;
            HostColorId = hostColorId;
            ItemSpinDegrees = itemSpinDegrees;
            TraceEnd = traceEnd;
            Viewport = viewport;
            HostBalloonType = hostBalloonType;
        }
    }
}
