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

        [Tooltip("Seconds the aim must stay sighted before the pulse begins (0 = immediate). Counts from " +
                 "the probe's hysteretic sighting and resets the moment sight is lost.")]
        [SerializeField] private float _delay;

        private Vector3 _restLocalScale;
        private float _currentDepth;
        private float _depthVelocity;
        private float _sightTime;

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

            // Onset delay: require _delay seconds of continuous (hysteretic) sight before the pulse
            // engages, so it lands a beat after the aim settles rather than the instant it grazes. The
            // timer resets the moment sight is lost; before it elapses the depth eases toward 0 (no pulse).
            var sighted = Probe != null && Probe.IsSighted.Value;
            _sightTime = sighted ? _sightTime + Time.unscaledDeltaTime : 0f;

            var target = _sightTime >= _delay ? _pulseScale * centrality : 0f;
            _currentDepth = Mathf.SmoothDamp(_currentDepth, target, ref _depthVelocity, _smoothTime);

            // Throb from rest up by the current depth — a positive grow-and-return, never below rest.
            // Unscaled so the pulse reads at a real rate regardless of time scale.
            var pulse = Mathf.Sin(Time.unscaledTime * _frequency * Tau) * 0.5f + 0.5f;
            _pulseTarget.localScale = _restLocalScale * (1f + _currentDepth * pulse);
        }

        protected override void ResetReaction()
        {
            _currentDepth = 0f;
            _depthVelocity = 0f;
            _sightTime = 0f;

            if (_pulseTarget != null)
            {
                _pulseTarget.localScale = _restLocalScale;
            }
        }
    }
}
