using DG.Tweening;
using UnityEngine;

namespace BalloonParty.UI.Notices
{
    /// <summary>
    ///     One short handwritten line on the canvas: written on in pencil where it's asked for, held, then
    ///     faded out. A new <see cref="Show" /> replaces the line showing.
    /// </summary>
    public class HandwrittenNotice : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private WriteOnText _writeOn;
        [SerializeField] [Range(0f, 1f)] private float _visibleAlpha = 0.85f;

        [Tooltip("Seconds the written line stays before it fades.")]
        [SerializeField] [Min(0f)] private float _holdDuration = 0.35f;
        [SerializeField] [Min(0f)] private float _fadeOutDuration = 0.25f;

        [Tooltip("Fade length when the line is cleared early, e.g. once what it announced has happened.")]
        [SerializeField] [Min(0f)] private float _clearDuration = 0.1f;

        private RectTransform _rect;
        private Sequence _sequence;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _group.alpha = 0f;
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }

        /// <summary>Writes <paramref name="text" /> at <paramref name="localPosition" /> in the parent's space.</summary>
        public void Show(string text, Vector2 localPosition)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _sequence?.Kill();
            _rect.localPosition = localPosition;
            _group.alpha = _visibleAlpha;
            _writeOn.Write(text);

            // Unscaled, like the write itself: a notice must finish in slow-motion beats too.
            _sequence = DOTween.Sequence()
                .AppendInterval(_writeOn.Duration + _holdDuration)
                .Append(_group.DOFade(0f, _fadeOutDuration))
                .SetUpdate(true);
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
    }
}
