using BalloonParty.Configuration.HeadsUp;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     One short handwritten line in world space (a <c>TextMeshPro</c>, not on a canvas), so it sits where
    ///     it happened through the camera rig's zoom and shake: written on in pencil, held, then faded out. A
    ///     new <see cref="Show(string, Vector2)" /> replaces the line showing. Written plain it keeps the label's
    ///     own look; written with a <see cref="HeadsUpStyle" /> it takes that colour, size and weight for this
    ///     line only.
    /// </summary>
    public class HandwrittenHeadsUp : MonoBehaviour
    {
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

        private TMP_Text _label;
        private Material _material;
        private Color _faceColor = Color.white;
        private Tween _fade;
        private float _alpha;

        // The label's look as built: what a plain Show writes in, and what a style scales from.
        private Color _baseColor;
        private float _baseFontSize;
        private FontStyles _baseFontStyle;

        /// <summary>The line is written or still fading.</summary>
        internal bool IsShowing => _alpha > 0f;

        /// <summary>The box the last written ink covers, in world space.</summary>
        internal Rect InkRect { get; private set; }

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            if (_label != null)
            {
                _baseColor = _label.color;
                _baseFontSize = _label.fontSize;
                _baseFontStyle = _label.fontStyle;
                ApplyHalo();
                // This line's own instance (the halo already made one): the fade writes to it.
                _material = _label.fontMaterial;
                _faceColor = _material.GetColor(ShaderUtilities.ID_FaceColor);
            }

            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            _fade?.Kill();
        }

        /// <summary>Writes <paramref name="text" /> at <paramref name="worldPosition" />.</summary>
        public void Show(string text, Vector2 worldPosition)
        {
            if (_label != null)
            {
                ApplyLook(_baseColor, _baseFontSize, _baseFontStyle);
            }

            Write(text, worldPosition);
        }

        /// <summary>
        ///     Writes <paramref name="text" /> in <paramref name="style" />, sized for an escalating
        ///     <paramref name="count" /> (1 = the first).
        /// </summary>
        public void Show(string text, Vector2 worldPosition, HeadsUpStyle style, int count = 1)
        {
            if (_label != null)
            {
                ApplyLook(style.Color, _baseFontSize * style.SizeAt(count), style.FontStyle);
            }

            Write(text, worldPosition);
        }

        /// <summary>
        ///     Writes <paramref name="text" /> in <paramref name="style" /> and keeps it up until
        ///     <see cref="Clear" />, for a state that lasts (the cannon's ready) rather than a moment.
        /// </summary>
        public void ShowUntilCleared(string text, Vector2 worldPosition, HeadsUpStyle style)
        {
            if (_label != null)
            {
                ApplyLook(style.Color, _baseFontSize * style.SizeAt(1), style.FontStyle);
            }

            Write(text, worldPosition, holdUntilCleared: true);
        }

        /// <summary>Moves the written line, ink and all, to <paramref name="worldPosition" />.</summary>
        internal void MoveTo(Vector2 worldPosition)
        {
            var shift = worldPosition - (Vector2)transform.position;
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            InkRect = new Rect(InkRect.position + shift, InkRect.size);
        }

        /// <summary>Fades the line out now, if one is showing.</summary>
        public void Clear()
        {
            // The fade is shorter than most writes: without this the pencil goes on scribbling over a line
            // that's no longer there.
            _writeOn.Stop();
            if (_alpha <= 0f)
            {
                return;
            }

            FadeOut(0f, _clearDuration);
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

        private void Write(string text, Vector2 worldPosition, bool holdUntilCleared = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _fade?.Kill();
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            SetAlpha(_visibleAlpha);
            _writeOn.Write(text);

            // The write has built the mesh, so the bounds hold every character, faded-in or not.
            var bounds = _label.textBounds;
            var scale = (Vector2)transform.lossyScale;
            InkRect = new Rect(worldPosition + Vector2.Scale(bounds.min, scale), Vector2.Scale(bounds.size, scale));

            if (!holdUntilCleared)
            {
                FadeOut(_writeOn.Duration + _holdDuration, _fadeOutDuration);
            }
        }

        // Unscaled, like the write itself: a heads-up must finish in slow-motion beats too.
        private void FadeOut(float delay, float duration)
        {
            _fade?.Kill();
            _fade = DOTween.To(() => _alpha, SetAlpha, 0f, duration)
                .SetDelay(delay)
                .SetUpdate(true);
        }

        // Through the material's face and outline colours, not the text's colour: setting that rebuilds the
        // mesh, which would undo the write-on's per-glyph reveal mid-stroke. The shader multiplies both by the
        // vertex colour the write-on drives.
        private void SetAlpha(float alpha)
        {
            _alpha = alpha;
            if (_material == null)
            {
                return;
            }

            var face = _faceColor;
            face.a *= alpha;
            _material.SetColor(ShaderUtilities.ID_FaceColor, face);
            var outline = _haloWidth > 0f ? _haloColor : Color.clear;
            outline.a *= alpha;
            _material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        }
    }
}
