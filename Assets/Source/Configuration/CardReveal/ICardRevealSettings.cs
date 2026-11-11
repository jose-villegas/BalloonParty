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

        /// <summary>How much of the slip shows through the whole front rubber, as a shadow inside the balloon: 0 to 1.</summary>
        float SlipShowThrough { get; }

        float RiseSeconds { get; }

        /// <summary>The whole balloon's beat at its place before it pops.</summary>
        float HoldBeforePopSeconds { get; }

        /// <summary>The frame of the pop on which the slip starts being written: once the rubber has opened.</summary>
        int WriteFromFrame { get; }

        /// <summary>How long the written slip stays once the pop is over.</summary>
        float SlipHoldSeconds { get; }

        float FadeOutSeconds { get; }

        /// <summary>
        ///     How far the two rubber layers part while the balloon moves, as a share of its distance from its
        ///     place: the front leads, the back lags, so the slip reads as inside it.
        /// </summary>
        float Parallax { get; }
    }
}
