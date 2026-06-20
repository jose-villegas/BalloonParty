using System;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI.Danger
{
    /// <summary>
    ///     Tints one or more sprites by the current danger level (0→1) sampled along a gradient — the
    ///     early-warning effect for running out of space — and optionally slides a container on Y. The
    ///     bound level is a target the view eases toward each frame, so colour and offset glide to new
    ///     values instead of snapping. A dumb view: <c>DangerGradientBinder</c> binds it to
    ///     <c>SpaceDanger.Level</c> at <c>Start</c>. Author the gradient (e.g. clear/green → amber → red)
    ///     and assign the target sprites in the inspector.
    /// </summary>
    internal class DangerGradientView : MonoBehaviour
    {
        private const float SettleEpsilon = 0.001f;

        [SerializeField] private Gradient _gradient;
        [SerializeField] private SpriteRenderer[] _targets;

        [Header("Translation")]
        [SerializeField] private Transform _container;
        [SerializeField] private float _offsetY;

        [Header("Easing")]
        [SerializeField] private float _lerpSpeed = 8f;

        private Vector3 _containerRestPosition;
        private float _currentLevel;
        private float _targetLevel;
        private IDisposable _subscription;

        private void Awake()
        {
            // Capture the rest position before any binding moves it, so the offset is always relative.
            if (_container != null)
            {
                _containerRestPosition = _container.localPosition;
            }
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentLevel, _targetLevel))
            {
                return;
            }

            // Frame-rate-independent ease toward the latest target; snap once it's effectively there.
            _currentLevel = Mathf.Lerp(_currentLevel, _targetLevel, 1f - Mathf.Exp(-_lerpSpeed * Time.deltaTime));
            if (Mathf.Abs(_currentLevel - _targetLevel) <= SettleEpsilon)
            {
                _currentLevel = _targetLevel;
            }

            ApplyVisual(_currentLevel);
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }

        public void Bind(IReadOnlyReactiveProperty<float> level)
        {
            _subscription?.Dispose();
            _subscription = level.Subscribe(value => _targetLevel = Mathf.Clamp01(value));

            // Subscribe fired with the current level — snap to it so we don't ease up from zero on bind.
            _currentLevel = _targetLevel;
            ApplyVisual(_currentLevel);
        }

        public void Unbind()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void ApplyVisual(float level)
        {
            var color = _gradient.Evaluate(level);

            foreach (var target in _targets)
            {
                if (target != null)
                {
                    target.color = color;
                }
            }

            if (_container != null)
            {
                var position = _containerRestPosition;
                position.y += _offsetY * level;
                _container.localPosition = position;
            }
        }
    }
}
