using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI
{
    /// <summary>Shows or hides a CanvasGroup based on whether it's day or night.</summary>
    internal class TimeOfDayVisibility : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private bool _visibleAtNight = true;
        [Tooltip("Fade duration when toggling visibility.")]
        [SerializeField] private float _fadeDuration = 0.4f;

        [Inject] private ITimeOfDayNight _timeOfDayNight;

        private float _targetAlpha;
        private float _currentAlpha;

        private void Awake()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void Start()
        {
            _targetAlpha = ShouldBeVisible() ? 1f : 0f;
            _currentAlpha = _targetAlpha;
            ApplyAlpha();
        }

        private void Update()
        {
            _targetAlpha = ShouldBeVisible() ? 1f : 0f;

            if (Mathf.Approximately(_currentAlpha, _targetAlpha))
            {
                return;
            }

            var speed = 1f / Mathf.Max(_fadeDuration, 0.001f);
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, speed * Time.unscaledDeltaTime);
            ApplyAlpha();
        }

        private bool ShouldBeVisible()
        {
            return _visibleAtNight ? _timeOfDayNight.IsNight : !_timeOfDayNight.IsNight;
        }

        private void ApplyAlpha()
        {
            _canvasGroup.alpha = _currentAlpha;
            _canvasGroup.interactable = _currentAlpha > 0f;
            _canvasGroup.blocksRaycasts = _currentAlpha > 0f;
        }
    }
}
