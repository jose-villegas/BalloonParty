using System;
using BalloonParty.Shared.Extensions;
using BalloonParty.UI.Binding;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI.Danger
{
    /// <summary>
    ///     Drives the early-warning effect from the danger level (0→1): tints sprites, grows the
    ///     top/bottom gradients toward the centre, and slides their containers.
    /// </summary>
    internal class DangerGradientView : MonoBehaviour, IReactiveBindable<float>
    {
        private const float SettleEpsilon = 0.001f;

        [SerializeField] private Gradient _gradient;
        [SerializeField] private SpriteRenderer[] _targets;

        [Header("Expansion")]
        [SerializeField] private SpriteRenderer[] _topGradients;
        [SerializeField] private float _topSizeIncrease;
        [SerializeField] private SpriteRenderer[] _bottomGradients;
        [SerializeField] private float _bottomSizeIncrease;

        [Header("Container translation")]
        [SerializeField] private Transform _topContainer;
        [SerializeField] private float _topContainerOffsetY;
        [SerializeField] private Transform _bottomContainer;
        [SerializeField] private float _bottomContainerOffsetY;

        [Header("Easing")]
        [SerializeField] private float _lerpSpeed = 8f;

        private Vector3[] _topRestPositions;
        private Vector2[] _topRestSizes;
        private Vector3[] _bottomRestPositions;
        private Vector2[] _bottomRestSizes;
        private Vector3 _topContainerRest;
        private Vector3 _bottomContainerRest;
        private float _currentLevel;
        private float _targetLevel;
        private IDisposable _subscription;

        private void Awake()
        {
            // Capture rest state before any binding grows the sprites.
            CaptureRest(_topGradients, out _topRestPositions, out _topRestSizes);
            CaptureRest(_bottomGradients, out _bottomRestPositions, out _bottomRestSizes);

            if (_topContainer != null)
            {
                _topContainerRest = _topContainer.localPosition;
            }

            if (_bottomContainer != null)
            {
                _bottomContainerRest = _bottomContainer.localPosition;
            }
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentLevel, _targetLevel))
            {
                return;
            }

            // Frame-rate-independent ease; snap once effectively there.
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

            // Snap to the current level so we don't ease up from zero on bind.
            _currentLevel = _targetLevel;
            ApplyVisual(_currentLevel);
        }

        public void Unbind()
        {
            LifecycleHelper.DisposeAndClear(ref _subscription);
        }

        private void ApplyVisual(float level)
        {
            var color = _gradient.Evaluate(level);
            _targets.SetColor(color);
            _topGradients.SetColor(color);
            _bottomGradients.SetColor(color);

            // Recentre by half the growth so the outer edge holds.
            Expand(_bottomGradients, _bottomRestPositions, _bottomRestSizes, _bottomSizeIncrease * level, 0.5f);
            Expand(_topGradients, _topRestPositions, _topRestSizes, _topSizeIncrease * level, -0.5f);

            _topContainer.SetLocalY(_topContainerRest.y + (_topContainerOffsetY * level));
            _bottomContainer.SetLocalY(_bottomContainerRest.y + (_bottomContainerOffsetY * level));
        }

        private static void Expand(
            SpriteRenderer[] renderers,
            Vector3[] restPositions,
            Vector2[] restSizes,
            float growth,
            float shiftRatio)
        {
            if (renderers == null)
            {
                return;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.size = restSizes[i].WithY(restSizes[i].y + growth);
                renderer.transform.SetLocalY(restPositions[i].y + (growth * shiftRatio));
            }
        }

        private static void CaptureRest(SpriteRenderer[] renderers, out Vector3[] positions, out Vector2[] sizes)
        {
            if (renderers == null)
            {
                positions = Array.Empty<Vector3>();
                sizes = Array.Empty<Vector2>();
                return;
            }

            positions = new Vector3[renderers.Length];
            sizes = new Vector2[renderers.Length];

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                positions[i] = renderers[i].transform.localPosition;
                sizes[i] = renderers[i].size;
            }
        }
    }
}
