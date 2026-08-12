using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Draws one item type's affected area as strokes, for the aim-time telegraph that shows what an
    ///     item along the predicted path would do. One implementation per <see cref="ItemType" />, resolved
    ///     by <see cref="Type" /> — the same registration shape as <see cref="IBalloonItem" />, so a new
    ///     item ships its preview beside its handler and no dispatch table needs editing.
    /// </summary>
    /// <remarks>
    ///     <see cref="BuildShape" /> is pure geometry: it appends points and never touches a renderer, the
    ///     pool, or <c>Time</c>. That keeps every figure edit-mode testable (as <c>TraceHitGeometry</c> and
    ///     <c>PaintTriangle</c> already are) and lets one driver animate all of them.
    ///     <para>
    ///         An implementation that has an existing config-free core for its geometry — <c>PaintTriangle</c>,
    ///         <c>LightningChain</c>, <c>BombBlast</c>, <c>LaserCross</c> — MUST build on it rather than
    ///         re-deriving the shape. A telegraph that computed its own version would drift from the effect
    ///         it advertises, the same failure <c>Shared/CircleContact</c> exists to prevent for the aim line.
    ///     </para>
    /// </remarks>
    internal interface IItemRangePreview
    {
        ItemType Type { get; }

        /// <summary>
        ///     Fills <paramref name="shape" /> (already cleared) with this item's figure in world space.
        ///     Emitting nothing is valid — it just means this item shows no board telegraph.
        /// </summary>
        void BuildShape(in ItemPreviewContext context, ItemPreviewShape shape);
    }
}
