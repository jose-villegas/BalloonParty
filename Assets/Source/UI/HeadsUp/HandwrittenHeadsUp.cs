using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     One short handwritten line on the canvas: written on in pencil where it's asked for, held, then
    ///     faded out. A new <see cref="Show(string, Vector2)" /> replaces the line showing. Written plain it
    ///     keeps the label's own look; written with a <see cref="HeadsUpStyle" /> it takes that colour, size and
    ///     weight for this line only.
    /// </summary>
    public class HandwrittenHeadsUp : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private WriteOnText _writeOn;
        [SerializeField] [Range(0f, 1f)] private float _visibleAlpha = 0.85f;

        [Tooltip("Seconds the written line stays before it fades.")]
        [SerializeField] [Min(0f)] private float _holdDuration = 0.35f;
        [SerializeField] [Min(0f)] private float _fadeOutDuration = 0.25f;

        [Tooltip("Fade length when the line is cleared early, e.g. once what it announced has happened.")]
        [SerializeField] [Min(0f)] private float _clearDuration = 0.1f;

        [Tooltip("A soft outline behind the ink, so dark lines read over the day sky and the night sky alike: " +
                 "pencil on a scrap of paper. Width 0 = none.")]
        [SerializeField] private Color _haloColor = new(1f, 0.973f, 0.906f);
        [SerializeField] [Range(0f, 1f)] private float _haloWidth = 0.25f;

        private RectTransform _rect;
        private TMP_Text _label;
        private Sequence _sequence;

        // The label's look as built: what a plain Show writes in, and what a style scales from.
        private Color _baseColor;
        private float _baseFontSize;
        private FontStyles _baseFontStyle;

        /// <summary>The line is written or still fading.</summary>
        internal bool IsShowing => _group.alpha > 0f;

        /// <summary>The box the last written ink covers, in the parent's space.</summary>
        internal Rect InkRect { get; private set; }

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group.alpha = 0f;
            _label = GetComponent<TMP_Text>();
            if (_label != null)
            {
                _baseColor = _label.color;
                _baseFontSize = _label.fontSize;
                _baseFontStyle = _label.fontStyle;
                ApplyHalo();
            }
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }

        /// <summary>Writes <paramref name="text" /> at <paramref name="localPosition" /> in the parent's space.</summary>
        public void Show(string text, Vector2 localPosition)
        {
            if (_label != null)
            {
                ApplyLook(_baseColor, _baseFontSize, _baseFontStyle);
            }

            Write(text, localPosition);
        }

        /// <summary>
        ///     Writes <paramref name="text" /> in <paramref name="style" />, sized for an escalating
        ///     <paramref name="count" /> (1 = the first).
        /// </summary>
        public void Show(string text, Vector2 localPosition, HeadsUpStyle style, int count = 1)
        {
            if (_label != null)
            {
                ApplyLook(style.Color, _baseFontSize * style.SizeAt(count), style.FontStyle);
            }

            Write(text, localPosition);
        }

        /// <summary>Moves the written line, ink and all, to <paramref name="localPosition" /> in the parent's space.</summary>
        internal void MoveTo(Vector2 localPosition)
        {
            var shift = localPosition - (Vector2)_rect.localPosition;
            _rect.localPosition = localPosition;
            InkRect = new Rect(InkRect.position + shift, InkRect.size);
        }

        /// <summary>Fades the line out now, if one is showing.</summary>
        public void Clear()
        {
            if (_group.alpha <= 0f)
            {
                return;
            }

            _sequence?.Kill();
            _sequence = DOTween.Sequence()
                .Append(_group.DOFade(0f, _clearDuration))
                .SetUpdate(true);
        }

        // Outline width and colour live on the font material: setting them gives this line its own instance,
        // a handful of draw calls for the few heads-up lines there are.
        private void ApplyHalo()
        {
            if (_haloWidth <= 0f)
            {
                return;
            }

            _label.outlineColor = _haloColor;
            _label.outlineWidth = _haloWidth;
        }

        private void ApplyLook(Color color, float fontSize, FontStyles fontStyle)
        {
            _label.color = color;
            _label.fontSize = fontSize;
            _label.fontStyle = fontStyle;
        }

        private void Write(string text, Vector2 localPosition)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _sequence?.Kill();
            _rect.localPosition = localPosition;
            _group.alpha = _visibleAlpha;
            _writeOn.Write(text);

            // The write has built the mesh, so the bounds hold every character, faded-in or not.
            var bounds = _label.textBounds;
            var scale = (Vector2)_rect.localScale;
            InkRect = new Rect(localPosition + Vector2.Scale(bounds.min, scale), Vector2.Scale(bounds.size, scale));

            // Unscaled, like the write itself: a heads-up must finish in slow-motion beats too.
            _sequence = DOTween.Sequence()
                .AppendInterval(_writeOn.Duration + _holdDuration)
                .Append(_group.DOFade(0f, _fadeOutDuration))
                .SetUpdate(true);
        }
    }
}
