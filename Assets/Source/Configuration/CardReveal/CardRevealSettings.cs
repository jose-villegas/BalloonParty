using System.Collections.Generic;
using UnityEngine;

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
        [SerializeField] [Min(1f)] private float _framesPerSecond = 30f;

        [Header("Playing")]
        [Tooltip("How much of the slip shows through the whole front rubber: a shadow inside the balloon.")]
        [SerializeField] [Range(0f, 1f)] private float _slipShowThrough = 0.12f;

        [SerializeField] [Min(0f)] private float _riseSeconds = 0.55f;
        [SerializeField] [Min(0f)] private float _holdBeforePopSeconds = 0.25f;

        [Tooltip("The pop's frame on which the slip starts being written: once the rubber has opened.")]
        [SerializeField] [Min(0)] private int _writeFromFrame = 4;

        [SerializeField] [Min(0f)] private float _slipHoldSeconds = 1.4f;
        [SerializeField] [Min(0f)] private float _fadeOutSeconds = 0.35f;

        [Tooltip("How far the rubber's two layers part while the balloon moves, as a share of its distance from " +
                 "its place: the front leads and the back lags, so the slip reads as inside it.")]
        [SerializeField] [Range(0f, 0.5f)] private float _parallax = 0.02f;

        public IReadOnlyList<Texture2D> PopSheets => _popSheets;
        public int PopCount => Mathf.Max(1, _popSheets.Length);
        public int RecentPopsAvoided => Mathf.Max(0, _recentPopsAvoided);
        public int SheetColumns => _sheetColumns;
        public int SheetRows => _sheetRows;
        public float FramesPerSecond => _framesPerSecond;
        public float SlipShowThrough => _slipShowThrough;
        public float RiseSeconds => _riseSeconds;
        public float HoldBeforePopSeconds => _holdBeforePopSeconds;
        public int WriteFromFrame => _writeFromFrame;
        public float SlipHoldSeconds => _slipHoldSeconds;
        public float FadeOutSeconds => _fadeOutSeconds;
        public float Parallax => _parallax;
    }
}
