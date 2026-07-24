using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     A <see cref="SightRampReaction"/> that throbs a transform's scale while the shot's aim is sighted
    ///     on this item — a warm, inviting "come get it" pulse whose depth scales with centrality, so it
    ///     grows as the aim centres and settles to rest off-aim. The positive-tone counterpart to
    ///     <see cref="SightShakeReaction"/> (which reads as a threat), for beneficial items like the shield.
    /// </summary>
    internal sealed class SightScalePulseReaction : SightRampReaction
    {
        private const float Tau = Mathf.PI * 2f;

        [Tooltip("Transform pulsed. Defaults to this object if left unset.")]
        [SerializeField] private Transform _pulseTarget;

        [Tooltip("Peak added scale fraction at full centrality (0.15 = throbs up to 1.15x its rest scale).")]
        [SerializeField] private float _pulseScale = 0.12f;

        [Tooltip("Throbs per second.")]
        [SerializeField] private float _frequency = 3f;

        [Tooltip("Seconds to ease the pulse depth in/out (SmoothDamp).")]
        [SerializeField] private float _smoothTime = 0.15f;

        private Vector3 _restLocalScale;
        private float _currentDepth;
        private float _depthVelocity;

        protected override void Awake()
        {
            base.Awake();

            if (_pulseTarget == null)
            {
                _pulseTarget = transform;
            }

            _restLocalScale = _pulseTarget.localScale;
        }

        protected override void OnSightTick(float centrality)
        {
            if (_pulseTarget == null)
            {
                return;
            }

            _currentDepth = Mathf.SmoothDamp(_currentDepth, _pulseScale * centrality, ref _depthVelocity, _smoothTime);

            // Throb from rest up by the current depth — a positive grow-and-return, never below rest.
            // Unscaled so the pulse reads at a real rate regardless of time scale.
            var pulse = Mathf.Sin(Time.unscaledTime * _frequency * Tau) * 0.5f + 0.5f;
            _pulseTarget.localScale = _restLocalScale * (1f + _currentDepth * pulse);
        }

        protected override void ResetReaction()
        {
            _currentDepth = 0f;
            _depthVelocity = 0f;

            if (_pulseTarget != null)
            {
                _pulseTarget.localScale = _restLocalScale;
            }
        }
    }
}
