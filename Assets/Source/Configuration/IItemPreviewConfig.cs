using BalloonParty.Configuration.Items;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>Per-item answer to "does the outward launch draw?", with a defer-to-shared default.</summary>
    public enum ItemPreviewBloomDraw
    {
        /// <summary>Use <see cref="IItemPreviewConfig.EmitDuringBloom" />.</summary>
        Inherit,

        Draw,
        Hide
    }

    /// <summary>
    ///     Per-item-type overrides for the range telegraph's motion. Every field is opt-in: left at its
    ///     neutral value the item draws with the shared defaults.
    /// </summary>
    /// <remarks>
    ///     Deliberately carries no colour. Every figure draws with the pen prefab's own material, so the
    ///     telegraph reads as one system rather than as N differently-tinted effects — and there is no
    ///     runtime tint path to keep in step with it.
    /// </remarks>
    public interface IItemPreviewStyle
    {
        /// <summary>
        ///     Whether this item's pens paint on their way out to the figure.
        /// </summary>
        /// <remarks>
        ///     Matters most for a figure that sits far from its host — Shield's bounce stub is over at a
        ///     wall, so a drawn approach is a long spoke across the board that buries the stub it was
        ///     meant to introduce. A figure centred on the host (Bomb's circle) has almost no approach to
        ///     draw and doesn't care.
        /// </remarks>
        ItemPreviewBloomDraw BloomDraw { get; }

        /// <summary>
        ///     Ribbon lifetime in seconds for this item's pens, or 0 to keep the prefab's authored value.
        /// </summary>
        /// <remarks>
        ///     The knob that decides how much of a figure is visible at once: a pen paints
        ///     <c>TraceSpeed × seconds</c> of world length before its tail fades, so a long figure (the
        ///     Laser's 40-unit corridors) needs far more than a short one (a Bomb circle a few units
        ///     around) to read as a complete shape rather than a travelling dash.
        /// </remarks>
        float RibbonSeconds { get; }
    }

    /// <summary>
    ///     Shield's own figure params. A per-item block rather than a field on
    ///     <see cref="IItemPreviewStyle" />, mirroring how <c>ItemSettings</c> nests <c>Bomb</c>/<c>Laser</c>/
    ///     <c>Paint</c> — a length only one figure reads has no business on the shared style.
    /// </summary>
    public interface IShieldPreviewSettings
    {
        /// <summary>
        ///     World length of the bounce stub drawn off the wall the aim line ends on. Long enough to
        ///     read as a direction, short enough not to become a second aim line.
        /// </summary>
        float StubLength { get; }
    }

    /// <summary>
    ///     Bomb's own figure params. A per-item block rather than a field on
    ///     <see cref="IItemPreviewStyle" />, mirroring how <c>ItemSettings</c> nests <c>Bomb</c>/<c>Laser</c>/
    ///     <c>Paint</c> — a number only one figure reads has no business on the shared style.
    /// </summary>
    public interface IBombPreviewSettings
    {
        /// <summary>
        ///     World units added to <c>BombSettings.Radius</c> purely for display, so the drawn circle can
        ///     be nudged without touching the blast itself. Can be negative to draw tighter than the true
        ///     radius; a positive value actually reads CLOSER to the real catchment, since <c>BombBlast</c>
        ///     catches an occupant whose centre lies within the radius plus that occupant's own radius, so
        ///     the drawn circle at the bare radius already understates the true reach.
        /// </summary>
        float RadiusOffset { get; }
    }

    /// <summary>
    ///     Paint's own figure params. A per-item block rather than a field on
    ///     <see cref="IItemPreviewStyle" />, mirroring how <c>ItemSettings</c> nests <c>Bomb</c>/<c>Laser</c>/
    ///     <c>Paint</c> — numbers only one figure reads have no business on the shared style.
    /// </summary>
    public interface IPaintPreviewSettings
    {
        /// <summary>
        ///     Display-only multiplier applied to both <c>PaintSettings.SpreadLength</c> and
        ///     <c>SpreadBaseWidth</c> when drawing the triangle, so the whole figure can be nudged without
        ///     touching the spread itself. <c>1</c> draws the true spread; below <c>1</c> draws it
        ///     tighter. Scales about the figure's own centre, so the triangle shrinks in place rather than
        ///     sliding toward the host as it gets smaller.
        /// </summary>
        float Scale { get; }
    }

    /// <summary>Tuning for the aim-time item range telegraph (@ref plan_item_range_preview).</summary>
    public interface IItemPreviewConfig
    {
        /// <summary>
        ///     Target world length of a dash, feeding <c>stride = DashLength + DashSpacing</c>, which in
        ///     turn derives each stroke's dash count. Not the literal painted length — the gap is the
        ///     pinned quantity (see <see cref="DashSpacing" />), so the dash a pen actually paints is
        ///     <c>slot − DashSpacing</c> and absorbs the per-stroke rounding error instead. Universal
        ///     across every figure — dashing is the one drawing style, not a per-item opt-in — so a Bomb
        ///     circle and Shield's stub target the same dash size.
        /// </summary>
        float DashLength { get; }

        /// <summary>
        ///     World length of blank arc between dashes — the pinned quantity a pen's gap always equals
        ///     exactly, regardless of stroke length, because rounding the dash count per stroke means the
        ///     slot is never exactly the stride; pinning the gap rather than the dash is what makes two
        ///     strokes of different length (e.g. Laser's two corridors) read as the same dash pattern. 0
        ///     reproduces a solid line: the stride collapses to <see cref="DashLength" />, so each pen's
        ///     slot equals its painted length and adjacent dashes touch with no gap — the zero-spacing
        ///     case, not a separate continuous mode.
        /// </summary>
        float DashSpacing { get; }

        /// <summary>
        ///     Hard ceiling on pens summed across a figure's strokes. A stroke's own dash count is derived
        ///     from its length, so a large figure (Laser's two ~40-unit corridors, ~160 units of combined
        ///     perimeter) would otherwise ask for hundreds of pooled <see cref="TrailRenderer" />s. Past
        ///     this cap every stroke's dashes are spaced out together instead of any part going undrawn.
        /// </summary>
        int MaxPens { get; }

        /// <summary>Seconds a pen takes to sweep out from the host to its stroke entry point.</summary>
        float BloomDuration { get; }

        /// <summary>
        ///     Extra degrees of arc a pen sweeps on the way out, on top of the straight-line bearing.
        ///     0 shoots radially outward; larger values curl the launch into a circular motion.
        /// </summary>
        float BloomSweepDegrees { get; }

        /// <summary>
        ///     Eases the outward bloom over its normalized duration — drives angle and radius together,
        ///     so the pen's whole spiral accelerates or settles as one.
        /// </summary>
        AnimationCurve BloomCurve { get; }

        /// <summary>World units per second a pen travels along its stroke once it starts tracing.</summary>
        float TraceSpeed { get; }

        /// <summary>
        ///     Whether the ribbon draws during the outward bloom. On, the launch spokes are visible; off,
        ///     only the figure is drawn — deploy spokes can bury a shape (the lesson recorded on
        ///     <c>FlyingTrail</c>'s own pen-up/pen-down helpers).
        /// </summary>
        bool EmitDuringBloom { get; }

        /// <summary>
        ///     Seconds a host must stay continuously sighted before its figure first appears. Restarts
        ///     whenever the sighted host changes, so sweeping the aim across a row of items shows nothing
        ///     until it settles on one. 0 shows the figure the instant a host is sighted, reproducing the
        ///     old no-delay behaviour.
        /// </summary>
        float SightDelaySeconds { get; }

        /// <summary>This item type's overrides, never null — an unauthored entry returns neutral values.</summary>
        IItemPreviewStyle StyleFor(ItemType type);

        /// <summary>Shield's own figure params.</summary>
        IShieldPreviewSettings Shield { get; }

        /// <summary>Bomb's own figure params.</summary>
        IBombPreviewSettings Bomb { get; }

        /// <summary>Paint's own figure params.</summary>
        IPaintPreviewSettings Paint { get; }
    }
}
