using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     A <see cref="SightRampReaction"/> that jitters a transform while the shot's aim is sighted on this
    ///     item — the amplitude scales with centrality, so it trembles harder as the aim centres and settles
    ///     to rest off-aim. Positional shake around the transform's authored local position, leaving rotation
    ///     free for a sibling <see cref="ProjectileFacingRotator"/>.
    /// </summary>
    internal sealed class SightShakeReaction : SightRampReaction
    {
        [Tooltip("Transform jittered. Defaults to this object if left unset.")]
        [SerializeField] private Transform _shakeTarget;

        [Tooltip("Peak local-space shake offset (units) at full centrality.")]
        [SerializeField] private float _amplitude = 0.05f;

        [Tooltip("Shake frequency — higher = a faster tremble.")]
        [SerializeField] private float _frequency = 25f;

        [Tooltip("Seconds to ease the shake amplitude in/out (SmoothDamp).")]
        [SerializeField] private float _smoothTime = 0.12f;

        private Vector3 _restLocalPosition;
        private float _currentAmplitude;
        private float _amplitudeVelocity;

        protected override void Awake()
        {
            base.Awake();

            if (_shakeTarget == null)
            {
                _shakeTarget = transform;
            }

            _restLocalPosition = _shakeTarget.localPosition;
        }

        protected override void OnSightTick(float centrality)
        {
            if (_shakeTarget == null)
            {
                return;
            }

            _currentAmplitude = Mathf.SmoothDamp(
                _currentAmplitude, _amplitude * centrality, ref _amplitudeVelocity, _smoothTime);

            // Perlin per-axis (independent seeds) gives a smooth, non-repeating tremble rather than a buzz.
            // Unscaled so the shake reads at a real rate regardless of time scale.
            var t = Time.unscaledTime * _frequency;
            var offsetX = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;
            var offsetY = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f;
            _shakeTarget.localPosition = _restLocalPosition + new Vector3(offsetX, offsetY, 0f) * _currentAmplitude;
        }

        protected override void ResetReaction()
        {
            _currentAmplitude = 0f;
            _amplitudeVelocity = 0f;

            if (_shakeTarget != null)
            {
                _shakeTarget.localPosition = _restLocalPosition;
            }
        }
    }
}
