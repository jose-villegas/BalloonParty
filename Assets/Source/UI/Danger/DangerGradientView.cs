using System;
using BalloonParty.Shared.Extensions;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI.Danger
{
    /// <summary>
    ///     Drives the early-warning effect from the danger level (0→1): tints sprites along a gradient,
    ///     grows the top/bottom gradient sprites toward the centre to simulate the gradient creeping over
    ///     the scenario, and slides top/bottom containers by a custom per-side Y offset. Each gradient
    ///     side has its own height increase; the sprite's Y is shifted by half that growth so its outer
    ///     edge stays anchored (bottom expands up, top expands down). The bound level is a target eased
    ///     toward each frame, so tint, growth and translation glide instead of snapping.
    ///
    ///     A dumb view: a <see cref="ReactivePropertyBinder{TView,TValue}" /> binds it to
    ///     <c>SpaceDanger.Level</c> at <c>Start</c>. Growing sprites must use a Sliced/Tiled draw mode for
    ///     <c>SpriteRenderer.size</c> to apply, and are assumed to have a centred pivot (hence the
    ///     half-growth recentre).
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
            // Capture rest size/position before any binding grows them, so the deltas stay relative.
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
            _targets.SetColor(color);
            _topGradients.SetColor(color);
            _bottomGradients.SetColor(color);

            // Bottom grows upward, top grows downward; recentre by half the growth so the outer edge holds.
            Expand(_bottomGradients, _bottomRestPositions, _bottomRestSizes, _bottomSizeIncrease * level, 0.5f);
            Expand(_topGradients, _topRestPositions, _topRestSizes, _topSizeIncrease * level, -0.5f);

            // Slide the containers by their own custom Y offset (sign is whatever the inspector value is).
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
