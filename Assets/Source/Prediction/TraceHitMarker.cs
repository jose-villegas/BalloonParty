using BalloonParty.Shared;
using UnityEngine;
using VContainer;

namespace BalloonParty.Prediction
{
    /// <summary>
    ///     View-only marker showing where the aim-prediction trace crosses this actor's circle. Sits on a
    ///     circular actor prefab (e.g. a balloon) alongside a <see cref="PredictionSightProbe"/>, which owns
    ///     the trace-vs-circle test — shared with any item <see cref="BalloonParty.Item.SightReaction"/> via
    ///     its own parent-search, so a balloon with an item runs that test once, not twice. This component
    ///     only turns the probe's signal into the marker's position/alpha.
    /// </summary>
    public class TraceHitMarker : MonoBehaviour
    {
        private const float DegenerateOffsetSqr = 1e-8f;

        [Tooltip("Sight source. Defaults to a PredictionSightProbe on this object if left unset.")]
        [SerializeField] private PredictionSightProbe _probe;

        [Tooltip("Child sprite positioned at the hit point; toggled on/off, never rotated or scaled.")]
        [SerializeField] private Transform _marker;

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

        [Inject] private IPredictionTraceConfig _config;

        private bool _isVisible;
        private float _baseAlpha = 1f;

        private void Awake()
        {
            if (_probe == null)
            {
                _probe = GetComponent<PredictionSightProbe>();
            }

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
            // Return), so OnEnable fires on every re-spawn — force-hide rather than trusting whatever the
            // probe (itself just re-enabled and mid-reset) happens to report this same frame.
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (_probe == null || !_probe.HasHit)
            {
                SetVisible(false);
                return;
            }

            var position = transform.position;
            var offset = (Vector3)_probe.SightPoint - position;
            if (offset.sqrMagnitude < DegenerateOffsetSqr)
            {
                SetVisible(false);
                return;
            }

            // The RADIAL direction from this actor's own centre to the surface crossing — not the probe's
            // SightDirection, which is the trace's travel direction and points along the shot, not outward.
            var hitDirection = offset.normalized;
            _marker.position = position + hitDirection * _markerOffset;

            if (_markerRenderer != null)
            {
                // RGB mirrors the trace line's configured colour (read per hit so the SO stays
                // live-tunable); only the alpha is ours — authored ceiling × centrality fade.
                var color = _config.LineColor;
                color.a = _baseAlpha * Mathf.Lerp(_minIntensity, 1f, _probe.Sight.Value);
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
