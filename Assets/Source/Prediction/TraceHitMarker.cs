using BalloonParty.Shared;
using UnityEngine;
using VContainer;

namespace BalloonParty.Prediction
{
    /// <summary>
    ///     View-only marker showing where the aim-prediction trace crosses this actor's circle. Sits on a
    ///     circular actor prefab (e.g. a balloon); reads <see cref="PredictionTraceProvider"/> instead of
    ///     computing or caching any trace state of its own.
    /// </summary>
    public class TraceHitMarker : MonoBehaviour
    {
        // 1cm — below this, the actor is considered stationary since the last evaluation.
        private const float PositionEpsilonSqr = 0.0001f;
        private const float DegenerateOffsetSqr = 1e-8f;

        [Tooltip("Child sprite positioned at the hit point; toggled on/off, never rotated or scaled.")]
        [SerializeField] private Transform _marker;

        [Tooltip("The actor's circle radius, in world units, that the trace must cross to register a hit.")]
        [SerializeField] private float _circleRadius;

        [Tooltip("Distance from the actor origin, along the hit direction, the marker sprite sits at.")]
        [SerializeField] private float _markerOffset;

        [Tooltip("Optional: the marker's sprite, alpha-scaled by how central the crossing is — a direct " +
                 "aim shows full strength, a tangential graze fades to Min Intensity. Leave empty to " +
                 "skip intensity modulation.")]
        [SerializeField] private SpriteRenderer _markerRenderer;

        [Tooltip("Alpha multiplier at the weakest hit (a tangential one-touch graze); a dead-centre aim " +
                 "is always 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _minIntensity = 0.25f;

        [Inject] private PredictionTraceProvider _traceProvider;
        [Inject] private IGameConfiguration _config;

        private bool _isVisible;
        private int _lastVersion;
        private Vector3 _lastPosition;
        private float _baseAlpha = 1f;

        private void Awake()
        {
            // The authored sprite alpha is the ceiling the centrality fade scales under — captured once,
            // before any modulation writes into the renderer's colour.
            if (_markerRenderer != null)
            {
                _baseAlpha = _markerRenderer.color.a;
            }
        }

        private void OnEnable()
        {
            // Pooled instances are reused by toggling the whole prefab's GameObject (PoolChannel<T>.Get/
            // Return), so OnEnable fires on every re-spawn. Force-hide and invalidate the cache here rather
            // than trusting a version/position that happened to carry over from the previous life — the
            // stale marker would otherwise be one frame away from a false show at the new spawn position.
            _lastVersion = int.MinValue;
            _lastPosition = transform.position;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!_traceProvider.IsActive || _traceProvider.Points.Count < 2)
            {
                SetVisible(false);
                return;
            }

            var position = transform.position;
            if (_traceProvider.Version == _lastVersion
                && (position - _lastPosition).sqrMagnitude < PositionEpsilonSqr)
            {
                return;
            }

            _lastVersion = _traceProvider.Version;
            _lastPosition = position;

            // The SURFACE entry point (line-circle intersection, first along the travel direction) —
            // not the perpendicular-closest point, which sits ~90° off anywhere but a tangential graze.
            var hasHit = TraceHitGeometry.TryFindSurfaceHit(
                _traceProvider.Points, position, _circleRadius,
                out var hitPoint, out var centrality);

            if (!hasHit)
            {
                SetVisible(false);
                return;
            }

            var offset = hitPoint - position;
            if (offset.sqrMagnitude < DegenerateOffsetSqr)
            {
                SetVisible(false);
                return;
            }

            var hitDirection = offset.normalized;
            _marker.position = position + hitDirection * _markerOffset;

            if (_markerRenderer != null)
            {
                // RGB mirrors the trace line's configured colour (read per hit so the SO stays
                // live-tunable); only the alpha is ours — authored ceiling × centrality fade.
                var color = _config.PredictionTraceColor;
                color.a = _baseAlpha * Mathf.Lerp(_minIntensity, 1f, centrality);
                _markerRenderer.color = color;
            }

            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (_isVisible == visible)
            {
                return;
            }

            _isVisible = visible;
            _marker.gameObject.SetActive(visible);
        }
    }
}
