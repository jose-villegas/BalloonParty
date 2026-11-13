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
        ///     Where the baked pops' sheets are (<c>Tools/art/balloon_pop</c>), as <c>Resources</c> paths: listed
        ///     rather than referenced, so they don't all load with the scene (<c>UI/CardReveal/PopSheetLoader</c>).
        ///     Each is the front layer's frames over the back layer's, holding the rubber's surface for
        ///     <c>BalloonParty/Sprite/LitFlipbook</c> to light. Empty until a pop is baked, and then the reveal
        ///     stays off.
        /// </summary>
        IReadOnlyList<string> PopSheetPaths { get; }

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

        /// <summary>
        ///     How far the balloon sways to each side as it rises, in canvas units, dying away by the pop.
        /// </summary>
        float RiseSway { get; }

        /// <summary>The time of one sway from side to side.</summary>
        float RiseSwaySeconds { get; }

        /// <summary>How far the balloon leans into its sway, in degrees.</summary>
        float RiseTilt { get; }

        /// <summary>
        ///     The burst of torn rubber flung far out as the balloon tears, tinted its colour: a pooled particle
        ///     system laid out in canvas units, parented to the reveal's canvas. None, and the pop has no shreds.
        /// </summary>
        ParticleSystem ShredsPrefab { get; }

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

        /// <summary>How long after <see cref="RibbonsGoneAtAscent" /> the ribbons stay before they are gone.</summary>
        float RibbonsAfterAscentSeconds { get; }

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
        ///     How far the torn rubber swings side to side as it falls, in frame widths at its widest. Each pop
        ///     varies it by <see cref="RubberSwingVariance" /> and starts it at a random point.
        /// </summary>
        float RubberSwing { get; }

        /// <summary>Seconds for one swing from side to side; the swing grows in over as long.</summary>
        float RubberSwingSeconds { get; }

        /// <summary>How far the rubber leans into its swing, in degrees.</summary>
        float RubberSwingTilt { get; }

        /// <summary>How much each pop varies the swing's reach and pace, as a share either way.</summary>
        float RubberSwingVariance { get; }

        /// <summary>
        ///     How fast the torn rubber turns as it falls, in degrees a second once its spin has built up over one
        ///     <see cref="RubberSwingSeconds" />. Each pop picks the direction and varies the speed.
        /// </summary>
        float RubberSpin { get; }

        /// <summary>
        ///     The white light its tear flashes into the scene light field, its radius in world units: it lights
        ///     the board around the reveal, and the reveal itself.
        /// </summary>
        float PopFlashRadius { get; }

        float PopFlashIntensity { get; }

        float PopFlashSeconds { get; }

        /// <summary>
        ///     Where each pop's puncture is in its first frame, as a share of the frame's width and height from its
        ///     bottom left, one per sheet in <see cref="PopSheetPaths" />' order: where the pushpin pricks, so the tear
        ///     opens from its needle. The bake prints them (<c>bake_pops.py</c>).
        /// </summary>
        IReadOnlyList<Vector2> PopPunctures { get; }

        /// <summary>The pushpin's plastic head's colour.</summary>
        Color PinHeadColor { get; }

        /// <summary>The pushpin's needle's colour.</summary>
        Color PinNeedleColor { get; }

        /// <summary>
        ///     Where the needle's tip is in the pin's image, as a share of its width and height from its bottom left:
        ///     the pin hangs from it. <c>bake_pin.py</c>'s <c>TIP</c>.
        /// </summary>
        Vector2 PinTip { get; }

        /// <summary>
        ///     The pin's image's size, in pop frame heights: <c>bake_pin.py</c>'s <c>PIN_VIEW</c> over the pops'
        ///     camera's <c>ortho_scale</c>, so the pin is drawn to the balloon's scale.
        /// </summary>
        float PinSize { get; }

        /// <summary>
        ///     The angle the pin is baked at, in degrees from straight up toward the right: its head's side of its
        ///     tip. It jabs along it. <c>bake_pin.py</c>'s <c>LEAN</c>.
        /// </summary>
        float PinLean { get; }

        /// <summary>How long the pin takes to come in from afar to hover by the puncture.</summary>
        float PinEnterSeconds { get; }

        /// <summary>How far away the pin comes in from, along its axis, in canvas units.</summary>
        float PinEnterDistance { get; }

        /// <summary>How far from the puncture the pin hovers, along its axis, in canvas units.</summary>
        float PinHoverDistance { get; }

        /// <summary>How much further the pin draws back before it jabs, in canvas units.</summary>
        float PinWindupDistance { get; }

        float PinWindupSeconds { get; }

        /// <summary>How long the jab takes, speeding up, from drawn back to in the rubber.</summary>
        float PinJabSeconds { get; }

        /// <summary>How far the needle goes into the rubber, in canvas units.</summary>
        float PinDepth { get; }

        /// <summary>How long the needle stays in as the rubber tears around it.</summary>
        float PinStaySeconds { get; }

        /// <summary>How far the burst throws the pin back out, in canvas units, as it fades.</summary>
        float PinRecoilDistance { get; }

        float PinLeaveSeconds { get; }
    }
}
