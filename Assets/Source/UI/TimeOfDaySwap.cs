using BalloonParty.Shared.SceneLight;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI
{
    /// <summary>Swaps an Image sprite between day and night with a fade transition.</summary>
    internal class TimeOfDaySwap : MonoBehaviour
    {
        [SerializeField] private Image _target;
        [SerializeField] private Sprite _daySprite;
        [SerializeField] private Sprite _nightSprite;
        [Tooltip("Total duration of the fade-out + fade-in swap transition.")]
        [SerializeField] private float _swapDuration = 0.4f;

        [Inject] private ITimeOfDayNight _timeOfDayNight;

        private bool _isNight;
        private float _swapTimer;
        private bool _swapping;
        private bool _swappedHalf;

        private void Start()
        {
            _isNight = _timeOfDayNight.IsNight;

            if (_target != null)
            {
                _target.sprite = _isNight ? _nightSprite : _daySprite;
            }
        }

        private void Update()
        {
            if (_target == null)
            {
                return;
            }

            var night = _timeOfDayNight.IsNight;
            if (night != _isNight && !_swapping)
            {
                _swapping = true;
                _swapTimer = 0f;
                _swappedHalf = false;
            }

            if (!_swapping)
            {
                return;
            }

            _swapTimer += Time.unscaledDeltaTime;
            var half = _swapDuration * 0.5f;

            if (!_swappedHalf && _swapTimer >= half)
            {
                _isNight = night;
                _target.sprite = _isNight ? _nightSprite : _daySprite;
                _swappedHalf = true;
            }

            var alpha = _swapTimer < half
                ? 1f - (_swapTimer / half)
                : (_swapTimer - half) / half;

            alpha = Mathf.Clamp01(alpha);
            var c = _target.color;
            c.a = alpha;
            _target.color = c;

            if (_swapTimer >= _swapDuration)
            {
                c.a = 1f;
                _target.color = c;
                _swapping = false;
            }
        }
    }
}
