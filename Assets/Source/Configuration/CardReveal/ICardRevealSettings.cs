using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration.CardReveal
{
    /// <summary>
    ///     The card reveal (<c>Game/CardReveal/</c>, <c>UI/CardReveal/</c>): which balloon pops there are, how one
    ///     is chosen, and how the reveal plays.
    /// </summary>
    internal interface ICardRevealSettings
    {
        /// <summary>
        ///     The baked pops' sheets (<c>Tools/art/balloon_pop</c>): each the front layer's frames over the back
        ///     layer's, holding the rubber's surface for <c>BalloonParty/Sprite/LitFlipbook</c> to light. Empty
        ///     until a pop is baked, and then the reveal stays off.
        /// </summary>
        IReadOnlyList<Texture2D> PopSheets { get; }

        /// <summary>How many pops there are to choose from: one per sheet, 1 or more.</summary>
        int PopCount { get; }

        /// <summary>How many of the last pops shown a new reveal never repeats, so a pop rarely comes back soon.</summary>
        int RecentPopsAvoided { get; }

        /// <summary>A layer's frames across one half of a sheet.</summary>
        int SheetColumns { get; }

        /// <summary>A layer's frames down one half of a sheet.</summary>
        int SheetRows { get; }

        float FramesPerSecond { get; }

        /// <summary>The prize balloon's colour: none of the board's, so it reads as the reward.</summary>
        Color BalloonColor { get; }

        /// <summary>
        ///     How much of the ribbons inside shows through the whole front rubber, as a shadow inside the
        ///     balloon: 0 to 1.
        /// </summary>
        float InsideShowThrough { get; }

        /// <summary>How long the balloon takes to float up to the top of its bob, just past its place.</summary>
        float RiseSeconds { get; }

        /// <summary>The whole balloon bobbing to rest at its place before it pops.</summary>
        float HoldBeforePopSeconds { get; }

        /// <summary>How far past its place the balloon floats before bobbing back, as a share of its rise.</summary>
        float RiseOvershoot { get; }

        /// <summary>How far the balloon sways to each side as it rises, in canvas units, dying away by the pop.</summary>
        float RiseSway { get; }

        /// <summary>The time of one sway from side to side.</summary>
        float RiseSwaySeconds { get; }

        /// <summary>How far the balloon leans into its sway, in degrees.</summary>
        float RiseTilt { get; }

        /// <summary>The frame of the pop on which the ribbons fly out: once the rubber has opened.</summary>
        int RibbonsFromFrame { get; }

        /// <summary>How long the ribbons take to burst out of the balloon to their spots.</summary>
        float RibbonBurstSeconds { get; }

        /// <summary>How long the ribbons take to fly off and fade at the end.</summary>
        float RibbonExitSeconds { get; }

        /// <summary>
        ///     The scale the ribbons swell to after the burst, so the card grows into view (1 or more), never wider
        ///     than the screen allows.
        /// </summary>
        float RibbonGrowth { get; }

        /// <summary>How long the ribbons take to swell to <see cref="RibbonGrowth" /> after the burst.</summary>
        float RibbonGrowSeconds { get; }

        /// <summary>
        ///     On a level-up, the share of the level's ascent by which the ribbons are gone: just before the next
        ///     level settles on screen.
        /// </summary>
        float RibbonsGoneAtAscent { get; }

        /// <summary>
        ///     How long the ribbons fly when no ascent runs with the reveal (a retry's bonus card, the cheat), from
        ///     the pop to gone.
        /// </summary>
        float RibbonSecondsWithoutAscent { get; }

        /// <summary>The ribbons' shortest flight, from the pop to gone, so they can always be read.</summary>
        float RibbonMinimumSeconds { get; }

        /// <summary>
        ///     How far the two rubber layers part while the balloon moves, as a share of its distance from its
        ///     place: the front leads, the back lags, so what is inside reads as inside it.
        /// </summary>
        float Parallax { get; }

        /// <summary>
        ///     The pop's frame on which the torn rubber starts to fall: the bake's <c>FALL_START</c>. The bake drops
        ///     the knot from there and its camera follows it, so the shreds trail above the knot in the frames
        ///     while the game moves the frames down.
        /// </summary>
        int RubberFallsFromFrame { get; }

        /// <summary>
        ///     How fast the torn rubber's fall speeds up, in frame heights per pop frame squared: the bake's
        ///     <c>FALL_ACCELERATION</c> over its camera's <c>ortho_scale</c>, so the game's motion is the one the
        ///     shreds trail in, at any frame rate.
        /// </summary>
        float RubberFallGravity { get; }

        /// <summary>
        ///     The pop's frame from which the bake's camera eases further down than the knot, lifting it toward the
        ///     frame's middle so the gathered rubber stays inside the frame: the bake's <c>RECENTRE_START</c> - 1.
        /// </summary>
        int RubberRecentreFromFrame { get; }

        /// <summary>How many frames that easing takes: the bake's <c>RECENTRE_FRAMES</c>.</summary>
        int RubberRecentreFrames { get; }

        /// <summary>
        ///     How far the camera eases down, in frame heights: the bake's <c>RECENTRE</c> over its camera's
        ///     <c>ortho_scale</c>. The game moves the frames down by it too, so the knot's path stays the fall's.
        /// </summary>
        float RubberRecentreHeight { get; }

        /// <summary>
        ///     The white light its tear flashes into the scene light field, its radius in world units: it lights
        ///     the board around the reveal, and the reveal itself.
        /// </summary>
        float PopFlashRadius { get; }

        float PopFlashIntensity { get; }

        float PopFlashSeconds { get; }
    }
}
