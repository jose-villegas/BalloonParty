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
        [Tooltip("The baked pops' sheets (Tools/art/balloon_pop), as paths under a Resources folder, so none loads " +
                 "with the scene: the reveal loads only the ones it needs. Each the front layer's frames over the " +
                 "back layer's, imported as linear data (sRGB off), no mipmaps, clamped. Empty, the reveal stays off.")]
        [SerializeField] private string[] _popSheetPaths = new string[0];

        [Tooltip("A new reveal never uses one of this many pops shown last. Capped at one fewer than the pops, " +
                 "so there is always a pop to choose.")]
        [SerializeField] [Min(0)] private int _recentPopsAvoided = 2;

        [Header("Sheet")]
        [SerializeField] [Min(1)] private int _sheetColumns = 8;
        [SerializeField] [Min(1)] private int _sheetRows = 6;
        [SerializeField] [Min(1f)] private float _framesPerSecond = 24f;

        [Tooltip("The prize balloon's colour: none of the board's, so it reads as the reward. The rubber is cel " +
                 "shaded in the scene's light over it, with a white highlight.")]
        [SerializeField] [ColorUsage(false)] private Color _balloonColor = new(0.8f, 0.82f, 0.87f, 1f);

        [Header("Playing")]
        [Tooltip("How much of the ribbons inside shows through the whole front rubber: a shadow inside the balloon.")]
        [FormerlySerializedAs("_slipShowThrough")]
        [SerializeField] [Range(0f, 1f)] private float _insideShowThrough = 0.12f;

        [Tooltip("The balloon floats up like a balloon let go: it rises past its place by Rise Overshoot (a share " +
                 "of its rise), at its highest after Rise Seconds, and bobs back to rest over Hold Before Pop " +
                 "Seconds, swaying side to side by Rise Sway (canvas units, one side to the other in Rise Sway " +
                 "Seconds) and leaning into the sway by Rise Tilt degrees, the sway dying away by the pop.")]
        [SerializeField] [Min(0f)] private float _riseSeconds = 0.8f;

        [SerializeField] [Min(0f)] private float _holdBeforePopSeconds = 0.5f;
        [SerializeField] [Range(0.001f, 0.5f)] private float _riseOvershoot = 0.06f;
        [SerializeField] [Min(0f)] private float _riseSway = 6f;
        [SerializeField] [Min(0.01f)] private float _riseSwaySeconds = 0.55f;
        [SerializeField] [Range(0f, 20f)] private float _riseTilt = 5f;

        [Tooltip("The torn rubber flung far out as the balloon tears, tinted its colour, fluttering down off the " +
                 "screen: a pooled particle system in canvas units (the canvas is 225 wide), made by the card " +
                 "reveal's builder. None, and the pop has no shreds.")]
        [SerializeField] private ParticleSystem _shredsPrefab;

        [Tooltip("How far the rubber's two layers part while the balloon moves, as a share of its distance from " +
                 "its place: the front leads and the back lags, so what is inside reads as inside it.")]
        [SerializeField] [Range(0f, 0.5f)] private float _parallax = 0.02f;

        [Tooltip("The pop's frame on which the torn rubber starts to fall: the bake's FALL_START. The bake drops " +
                 "the knot from there with its camera following, so the shreds trail above it in the frames.")]
        [SerializeField] [Min(0)] private int _rubberFallsFromFrame = 4;

        [Tooltip("How fast the torn rubber's fall speeds up, in frame heights per pop frame squared: the bake's " +
                 "FALL_ACCELERATION over its camera's ortho_scale (3.1), so the frames' trailing shreds match the " +
                 "motion at any frame rate.")]
        [SerializeField] [Min(0f)] private float _rubberFallGravity = 0.0045f;

        [Tooltip("Once the rubber gathers, the bake's camera eases further down than the knot, lifting it toward " +
                 "the frame's middle so the rubber stays inside the frame; the game moves the frames down by the " +
                 "same, so the knot's path stays the fall's. From this frame (the bake's RECENTRE_START - 1), over " +
                 "Rubber Recentre Frames (RECENTRE_FRAMES), by Rubber Recentre Height in frame heights (RECENTRE " +
                 "over the camera's ortho_scale, 3.1).")]
        [SerializeField] [Min(0)] private int _rubberRecentreFromFrame = 18;

        [Tooltip("How many frames the bake's camera takes to ease down: its RECENTRE_FRAMES.")]
        [SerializeField] [Min(1)] private int _rubberRecentreFrames = 12;

        [Tooltip("How far the bake's camera eases down, in frame heights: its RECENTRE over ortho_scale (3.1).")]
        [SerializeField] [Min(0f)] private float _rubberRecentreHeight = 0.52f;

        [Tooltip("How far the torn rubber swings side to side as it falls, in frame widths at its widest, the way " +
                 "a scrap of rubber flutters down. Each pop varies it (Rubber Swing Variance) and starts it at a " +
                 "random point, so no two fall alike. 0 = it falls straight.")]
        [SerializeField] [Min(0f)] private float _rubberSwing = 0.15f;

        [Tooltip("Seconds for one swing from one side to the other. The swing also grows in over this long, " +
                 "since the pop starts square at its place.")]
        [SerializeField] [Min(0.01f)] private float _rubberSwingSeconds = 0.4f;

        [Tooltip("How far the rubber leans into its swing, in degrees.")]
        [SerializeField] [Range(0f, 45f)] private float _rubberSwingTilt = 15f;

        [Tooltip("How much each pop varies the swing's reach and pace, as a share either way: 0.3 = up to 30% " +
                 "wider or narrower, faster or slower.")]
        [SerializeField] [Range(0f, 0.9f)] private float _rubberSwingVariance = 0.3f;

        [Tooltip("How fast the torn rubber turns as it falls, in degrees a second once it has picked up its spin " +
                 "(over one swing, so the fall starts square). Each pop turns it one way or the other at random " +
                 "and varies the speed by Rubber Swing Variance. 0 = it only leans with its swing.")]
        [SerializeField] [Min(0f)] private float _rubberSpin = 120f;

        [Header("Light")]
        [Tooltip("When the rubber tears, it flashes a white light into the scene light field, lighting the " +
                 "board around it (and the ribbons), and stirs the disturbance field as a board pop does. Its " +
                 "radius in world units, its strength, and how long it lasts.")]
        [SerializeField] [Min(0f)] private float _popFlashRadius = 2.4f;

        [SerializeField] [Min(0f)] private float _popFlashIntensity = 2.5f;
        [SerializeField] [Min(0f)] private float _popFlashSeconds = 0.3f;

        [Header("Ribbons")]
        [Tooltip("The pop's frame on which the ribbons fly out: once the rubber has opened.")]
        [FormerlySerializedAs("_writeFromFrame")]
        [SerializeField] [Min(0)] private int _ribbonsFromFrame = 8;

        [SerializeField] [Min(0f)] private float _ribbonBurstSeconds = 0.4f;
        [SerializeField] [Min(0f)] private float _ribbonExitSeconds = 0.5f;

        [Tooltip("A bonus that lands on a card at its limit: how long its struck title stands once the capped " +
                 "ribbon has burst out, then how long it takes to dissolve.")]
        [SerializeField] [Min(0f)] private float _cappedDissolveDelay = 0.5f;

        [SerializeField] [Min(0f)] private float _cappedDissolveSeconds = 0.9f;

        [Tooltip("After the burst the ribbons swell to this scale, easing in over Ribbon Grow Seconds, so the card " +
                 "grows into view. Never wider than the screen allows.")]
        [SerializeField] [Min(1f)] private float _ribbonGrowth = 1.3f;

        [SerializeField] [Min(0f)] private float _ribbonGrowSeconds = 1.2f;

        [Tooltip("On a level-up, the share of the level's ascent by which the ribbons are gone: just before the " +
                 "next level settles on screen. Its balloons come in at the ascent's spawn cue (0.6).")]
        [SerializeField] [Range(0f, 1f)] private float _ribbonsGoneAtAscent = 0.82f;

        [Tooltip("Seconds the ribbons stay after that share of the ascent.")]
        [SerializeField] [Min(0f)] private float _ribbonsAfterAscentSeconds = 0.5f;

        [Tooltip("With no ascent (a retry's bonus card, the cheat): seconds from the pop until the ribbons are gone.")]
        [SerializeField] [Min(0f)] private float _ribbonSecondsWithoutAscent = 3.9f;

        [Tooltip("The ribbons' shortest flight, from the pop until gone, so they can always be read.")]
        [SerializeField] [Min(0f)] private float _ribbonMinimumSeconds = 2.9f;

        [Header("Pushpin")]
        [Tooltip("Where each pop's puncture is in its first frame, as a share of the frame from its bottom left, " +
                 "one per pop sheet in order: where the pushpin pricks, so the tear opens from its needle. The " +
                 "bake prints them (bake_pops.py). A pop with none is pricked at the top of the balloon's front.")]
        [SerializeField] private Vector2[] _popPunctures = { new(0.511f, 0.803f) };

        [SerializeField] [ColorUsage(false)] private Color _pinHeadColor = new(0.86f, 0.16f, 0.2f, 1f);
        [SerializeField] [ColorUsage(false)] private Color _pinNeedleColor = new(0.78f, 0.8f, 0.84f, 1f);

        [Tooltip("The pin's baked image (Tools/art/balloon_pop/bake_pin.py): where its needle's tip is, as a share " +
                 "of the image from its bottom left (TIP); its size in pop frame heights (PIN_VIEW over the pops' " +
                 "ortho_scale, 3.1); and the angle it is baked at, degrees from straight up toward its head (LEAN). " +
                 "The bake prints them.")]
        [SerializeField] private Vector2 _pinTip = new(0.25f, 0.15f);

        [SerializeField] [Min(0f)] private float _pinSize = 0.3226f;
        [SerializeField] [Range(-90f, 90f)] private float _pinLean = 35f;

        [Tooltip("The pin comes in from Pin Enter Distance away along its axis (canvas units) to hover Pin Hover " +
                 "Distance from the puncture, over Pin Enter Seconds; draws back Pin Windup Distance more; jabs in, " +
                 "speeding up, Pin Depth into the rubber; stays Pin Stay Seconds as the rubber tears; then the burst " +
                 "throws it back Pin Recoil Distance as it fades. It goes in as the balloon comes to rest, and the " +
                 "pop starts then; a pin slower than the rise and the hold holds the balloon at rest until it is " +
                 "in.")]
        [SerializeField] [Min(0f)] private float _pinEnterSeconds = 0.3f;

        [SerializeField] [Min(0f)] private float _pinEnterDistance = 120f;
        [SerializeField] [Min(0f)] private float _pinHoverDistance = 12f;
        [SerializeField] [Min(0f)] private float _pinWindupDistance = 10f;
        [SerializeField] [Min(0f)] private float _pinWindupSeconds = 0.18f;
        [SerializeField] [Min(0f)] private float _pinJabSeconds = 0.06f;
        [SerializeField] [Min(0f)] private float _pinDepth = 3f;
        [SerializeField] [Min(0f)] private float _pinStaySeconds = 0.08f;
        [SerializeField] [Min(0f)] private float _pinRecoilDistance = 36f;
        [SerializeField] [Min(0f)] private float _pinLeaveSeconds = 0.4f;

        public IReadOnlyList<string> PopSheetPaths => _popSheetPaths;
        public int PopCount => Mathf.Max(1, _popSheetPaths.Length);
        public int RecentPopsAvoided => Mathf.Max(0, _recentPopsAvoided);
        public int SheetColumns => _sheetColumns;
        public int SheetRows => _sheetRows;
        public float FramesPerSecond => _framesPerSecond;
        public Color BalloonColor => _balloonColor;
        public float InsideShowThrough => _insideShowThrough;
        public float RiseSeconds => _riseSeconds;
        public float HoldBeforePopSeconds => _holdBeforePopSeconds;
        public float RiseOvershoot => _riseOvershoot;
        public float RiseSway => _riseSway;
        public float RiseSwaySeconds => _riseSwaySeconds;
        public float RiseTilt => _riseTilt;
        public ParticleSystem ShredsPrefab => _shredsPrefab;
        public int RibbonsFromFrame => _ribbonsFromFrame;
        public float RibbonBurstSeconds => _ribbonBurstSeconds;
        public float RibbonExitSeconds => _ribbonExitSeconds;
        public float CappedDissolveDelay => _cappedDissolveDelay;
        public float CappedDissolveSeconds => _cappedDissolveSeconds;
        public float RibbonGrowth => _ribbonGrowth;
        public float RibbonGrowSeconds => _ribbonGrowSeconds;
        public float RibbonsGoneAtAscent => _ribbonsGoneAtAscent;
        public float RibbonsAfterAscentSeconds => _ribbonsAfterAscentSeconds;
        public float RibbonSecondsWithoutAscent => _ribbonSecondsWithoutAscent;
        public float RibbonMinimumSeconds => _ribbonMinimumSeconds;
        public float Parallax => _parallax;
        public int RubberFallsFromFrame => _rubberFallsFromFrame;
        public float RubberFallGravity => _rubberFallGravity;
        public int RubberRecentreFromFrame => _rubberRecentreFromFrame;
        public int RubberRecentreFrames => _rubberRecentreFrames;
        public float RubberRecentreHeight => _rubberRecentreHeight;
        public float RubberSwing => _rubberSwing;
        public float RubberSwingSeconds => _rubberSwingSeconds;
        public float RubberSwingTilt => _rubberSwingTilt;
        public float RubberSwingVariance => _rubberSwingVariance;
        public float RubberSpin => _rubberSpin;
        public float PopFlashRadius => _popFlashRadius;
        public float PopFlashIntensity => _popFlashIntensity;
        public float PopFlashSeconds => _popFlashSeconds;
        public IReadOnlyList<Vector2> PopPunctures => _popPunctures;
        public Color PinHeadColor => _pinHeadColor;
        public Color PinNeedleColor => _pinNeedleColor;
        public Vector2 PinTip => _pinTip;
        public float PinSize => _pinSize;
        public float PinLean => _pinLean;
        public float PinEnterSeconds => _pinEnterSeconds;
        public float PinEnterDistance => _pinEnterDistance;
        public float PinHoverDistance => _pinHoverDistance;
        public float PinWindupDistance => _pinWindupDistance;
        public float PinWindupSeconds => _pinWindupSeconds;
        public float PinJabSeconds => _pinJabSeconds;
        public float PinDepth => _pinDepth;
        public float PinStaySeconds => _pinStaySeconds;
        public float PinRecoilDistance => _pinRecoilDistance;
        public float PinLeaveSeconds => _pinLeaveSeconds;
    }
}
