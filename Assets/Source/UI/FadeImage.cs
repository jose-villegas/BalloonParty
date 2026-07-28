using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI
{
    /// <summary>Fades an Image's alpha to a target over a configurable duration. Optionally plays on Awake.</summary>
    internal sealed class FadeImage : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private float _targetAlpha;
        [SerializeField] private float _duration = 0.5f;
        [SerializeField] private bool _playOnAwake = true;

        private float _currentAlpha;
        private float _goal;
        private bool _active;

        private void Awake()
        {
            if (_image == null)
            {
                _image = GetComponent<Image>();
            }

            _currentAlpha = _image.color.a;
            _goal = _currentAlpha;

            if (_playOnAwake)
            {
                FadeTo(_targetAlpha);
            }
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            var speed = 1f / Mathf.Max(_duration, 0.001f);
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, _goal, speed * Time.unscaledDeltaTime);
            ApplyAlpha();

            if (Mathf.Approximately(_currentAlpha, _goal))
            {
                _active = false;
            }
        }

        public void FadeTo(float alpha)
        {
            _goal = alpha;
            _active = true;
        }

        public void FadeTo(float alpha, float seconds)
        {
            _duration = seconds;
            FadeTo(alpha);
        }

        private void ApplyAlpha()
        {
            var color = _image.color;
            color.a = _currentAlpha;
            _image.color = color;
        }
    }
}
