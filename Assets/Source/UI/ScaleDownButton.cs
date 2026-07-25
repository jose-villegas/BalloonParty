using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace BalloonParty.UI
{
    /// <summary>
    ///     On click, tweens a target's scale toward <see cref="_targetScale" /> and only then fires
    ///     <see cref="_onScaleComplete" /> — so a button's real actions run after the press animation,
    ///     not the instant it's clicked. Move the deferred actions off the Button's own On Click and
    ///     onto this component's On Scale Complete.
    /// </summary>
    [DisallowMultipleComponent]
    internal class ScaleDownButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _targetScale = new(0.9f, 0.9f, 1f);
        [SerializeField] private float _duration = 0.1f;
        [SerializeField] private Ease _ease = Ease.OutQuad;

        // Launch/menu screens can run at a ramped or zero timescale, so drive the press off unscaled time.
        [SerializeField] private bool _useUnscaledTime = true;

        [SerializeField] private UnityEvent _onScaleComplete;

        private Tween _tween;
        private bool _playing;

        private void Awake()
        {
            if (_target == null)
            {
                _target = transform;
            }
        }

        private void OnDisable()
        {
            _tween?.Kill();
            _tween = null;
            _playing = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Play();
        }

        // Public so it can equally be wired to a Button's On Click; the guard makes a double entry
        // (both the click handler and an On Click call) a no-op rather than a stacked tween.
        public void Play()
        {
            if (_playing)
            {
                return;
            }

            _playing = true;
            _tween?.Kill();
            _tween = _target.DOScale(_targetScale, _duration)
                .SetEase(_ease)
                .SetUpdate(_useUnscaledTime)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    _playing = false;
                    _onScaleComplete?.Invoke();
                });
        }
    }
}
