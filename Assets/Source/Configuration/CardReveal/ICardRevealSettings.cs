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

        /// <summary>
        ///     How much of the ribbons inside shows through the whole front rubber, as a shadow inside the
        ///     balloon: 0 to 1.
        /// </summary>
        float InsideShowThrough { get; }

        float RiseSeconds { get; }

        /// <summary>The whole balloon's beat at its place before it pops.</summary>
        float HoldBeforePopSeconds { get; }

        /// <summary>The frame of the pop on which the ribbons fly out: once the rubber has opened.</summary>
        int RibbonsFromFrame { get; }

        /// <summary>How long the ribbons take to burst out of the balloon to their spots.</summary>
        float RibbonBurstSeconds { get; }

        /// <summary>How long the ribbons take to fly off and fade at the end.</summary>
        float RibbonExitSeconds { get; }

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
        ///     The pop's frame on which the torn rubber starts to fall. The bake keeps the balloon's neck where it was
        ///     until its last frames, so the fall is the game's.
        /// </summary>
        int RubberFallsFromFrame { get; }

        /// <summary>How fast the torn rubber's fall speeds up, in canvas units per second squared.</summary>
        float RubberFallGravity { get; }
    }
}
