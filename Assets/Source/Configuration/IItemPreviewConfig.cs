using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Shield's own figure params. A per-item block, mirroring how <c>ItemSettings</c> nests
    ///     <c>Bomb</c>/<c>Laser</c>/<c>Paint</c> — a length only one figure reads has no business on the
    ///     shared config.
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
    ///     Bomb's own figure params. A per-item block, mirroring how <c>ItemSettings</c> nests
    ///     <c>Bomb</c>/<c>Laser</c>/<c>Paint</c> — a number only one figure reads has no business on the
    ///     shared config.
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
    ///     Paint's own figure params. A per-item block, mirroring how <c>ItemSettings</c> nests
    ///     <c>Bomb</c>/<c>Laser</c>/<c>Paint</c> — numbers only one figure reads have no business on the
    ///     shared config.
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
        ///     Hard ceiling on pens summed across a figure's strokes AND their approach cascades (the
        ///     dashed comet tail each leading leg draws in with). A stroke's own dash count is derived from
        ///     its length, so a large figure (Laser's two ~40-unit corridors, ~160 units of combined
        ///     perimeter) would otherwise ask for hundreds of pooled <see cref="TrailRenderer" />s. Past
        ///     this cap every stroke's dashes are spaced out together instead of any part going undrawn —
        ///     the figure's own dashes claim their share first, so a long approach can only ever spend what
        ///     the figure didn't need.
        /// </summary>
        int MaxPens { get; }

        /// <summary>
        ///     Seconds the figure's FURTHEST-travelling pen takes to reach its dash slot, so every figure
        ///     — however large — completes its draw-in in the same time. Nearer pens cover less arc under
        ///     the same shared clock and so arrive sooner, which is what makes the figure read as drawing
        ///     itself in one stroke at a time rather than fading in all at once.
        /// </summary>
        float BloomDuration { get; }

        /// <summary>
        ///     Eases the shared bloom clock over its normalized duration — shapes how the drawing tip
        ///     accelerates and settles, applied once to the whole figure rather than per pen.
        /// </summary>
        AnimationCurve BloomCurve { get; }

        /// <summary>World units per second a pen travels along its stroke once it starts tracing.</summary>
        float TraceSpeed { get; }

        /// <summary>
        ///     Seconds a host must stay continuously sighted before its figure first appears. Restarts
        ///     whenever the sighted host changes, so sweeping the aim across a row of items shows nothing
        ///     until it settles on one. 0 shows the figure the instant a host is sighted, reproducing the
        ///     old no-delay behaviour.
        /// </summary>
        float SightDelaySeconds { get; }

        /// <summary>Shield's own figure params.</summary>
        IShieldPreviewSettings Shield { get; }

        /// <summary>Bomb's own figure params.</summary>
        IBombPreviewSettings Bomb { get; }

        /// <summary>Paint's own figure params.</summary>
        IPaintPreviewSettings Paint { get; }
    }
}
