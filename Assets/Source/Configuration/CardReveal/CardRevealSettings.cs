using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace BalloonParty.Configuration.CardReveal
{
    /// <summary>
    ///     The card reveal's settings. Unassigned on the game scope, a default instance stands in, which has no
    ///     pops, so the reveal stays off (<c>Plans/PLAN-CardReveal.md</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "Configuration/Card Reveal Settings", fileName = "CardRevealSettings")]
    internal sealed class CardRevealSettings : ScriptableObject, ICardRevealSettings
    {
        [Tooltip("The baked pops (Tools/art/balloon_pop): one sheet each, the front layer's frames over the back " +
                 "layer's. Imported as linear data (sRGB off), no mipmaps, clamped. Empty, the reveal stays off.")]
        [SerializeField] private Texture2D[] _popSheets = new Texture2D[0];

        [Tooltip("A new reveal never uses one of this many pops shown last. Capped at one fewer than the pops, " +
                 "so there is always a pop to choose.")]
        [SerializeField] [Min(0)] private int _recentPopsAvoided = 2;

        [Header("Sheet")]
        [SerializeField] [Min(1)] private int _sheetColumns = 6;
        [SerializeField] [Min(1)] private int _sheetRows = 4;
        [SerializeField] [Min(1f)] private float _framesPerSecond = 24f;

        [Header("Playing")]
        [Tooltip("How much of the ribbons inside shows through the whole front rubber: a shadow inside the balloon.")]
        [FormerlySerializedAs("_slipShowThrough")]
        [SerializeField] [Range(0f, 1f)] private float _insideShowThrough = 0.12f;

        [SerializeField] [Min(0f)] private float _riseSeconds = 0.45f;
        [SerializeField] [Min(0f)] private float _holdBeforePopSeconds = 0.15f;

        [Tooltip("How far the rubber's two layers part while the balloon moves, as a share of its distance from " +
                 "its place: the front leads and the back lags, so what is inside reads as inside it.")]
        [SerializeField] [Range(0f, 0.5f)] private float _parallax = 0.02f;

        [Tooltip("The pop's frame on which the torn rubber starts to fall: the bake keeps its neck where it was, " +
                 "so without this the pop plays out in one place.")]
        [SerializeField] [Min(0)] private int _rubberFallsFromFrame = 3;

        [Tooltip("How fast the torn rubber's fall speeds up, in canvas units per second squared (the canvas is " +
                 "225 wide).")]
        [SerializeField] [Min(0f)] private float _rubberFallGravity = 380f;

        [Header("Ribbons")]
        [Tooltip("The pop's frame on which the ribbons fly out: once the rubber has opened.")]
        [FormerlySerializedAs("_writeFromFrame")]
        [SerializeField] [Min(0)] private int _ribbonsFromFrame = 4;

        [SerializeField] [Min(0f)] private float _ribbonBurstSeconds = 0.4f;
        [SerializeField] [Min(0f)] private float _ribbonExitSeconds = 0.5f;

        [Tooltip("After the burst the ribbons swell to this scale, easing in over Ribbon Grow Seconds, so the card " +
                 "grows into view. Never wider than the screen allows.")]
        [SerializeField] [Min(1f)] private float _ribbonGrowth = 1.3f;

        [SerializeField] [Min(0f)] private float _ribbonGrowSeconds = 1.2f;

        [Tooltip("On a level-up, the share of the level's ascent by which the ribbons are gone: just before the " +
                 "next level settles on screen. Its balloons come in at the ascent's spawn cue (0.6).")]
        [SerializeField] [Range(0f, 1f)] private float _ribbonsGoneAtAscent = 0.82f;

        [Tooltip("With no ascent (a retry's bonus card, the cheat): seconds from the pop until the ribbons are gone.")]
        [SerializeField] [Min(0f)] private float _ribbonSecondsWithoutAscent = 3.4f;

        [Tooltip("The ribbons' shortest flight, from the pop until gone, so they can always be read.")]
        [SerializeField] [Min(0f)] private float _ribbonMinimumSeconds = 2.4f;

        public IReadOnlyList<Texture2D> PopSheets => _popSheets;
        public int PopCount => Mathf.Max(1, _popSheets.Length);
        public int RecentPopsAvoided => Mathf.Max(0, _recentPopsAvoided);
        public int SheetColumns => _sheetColumns;
        public int SheetRows => _sheetRows;
        public float FramesPerSecond => _framesPerSecond;
        public float InsideShowThrough => _insideShowThrough;
        public float RiseSeconds => _riseSeconds;
        public float HoldBeforePopSeconds => _holdBeforePopSeconds;
        public int RibbonsFromFrame => _ribbonsFromFrame;
        public float RibbonBurstSeconds => _ribbonBurstSeconds;
        public float RibbonExitSeconds => _ribbonExitSeconds;
        public float RibbonGrowth => _ribbonGrowth;
        public float RibbonGrowSeconds => _ribbonGrowSeconds;
        public float RibbonsGoneAtAscent => _ribbonsGoneAtAscent;
        public float RibbonSecondsWithoutAscent => _ribbonSecondsWithoutAscent;
        public float RibbonMinimumSeconds => _ribbonMinimumSeconds;
        public float Parallax => _parallax;
        public int RubberFallsFromFrame => _rubberFallsFromFrame;
        public float RubberFallGravity => _rubberFallGravity;
    }
}
