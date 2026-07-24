using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     A <see cref="SightRampReaction"/> that dampens a PierceConeSpiral renderer's Climb Speed
    ///     (<c>_Speed</c>) when the shot's aim isn't sighted on this item. The material's authored speed is
    ///     the ceiling (sighted = full speed); off-aim it eases down to <see cref="_outOfSightFactor"/> of it,
    ///     so the drill idles slow and spins up to its normal rate as the aim centres — never faster.
    /// </summary>
    internal sealed class PredictionSightSpiral : SightRampReaction
    {
        private static readonly int SpeedId = Shader.PropertyToID("_Speed");

        [Tooltip("Renderer carrying the PierceConeSpiral material whose Climb Speed (_Speed) is driven.")]
        [SerializeField] private Renderer _spiralRenderer;

        [Tooltip("Fraction of the material's authored Climb Speed used off-aim (0 = frozen, 1 = no damping).")]
        [Range(0f, 1f)]
        [SerializeField] private float _outOfSightFactor = 0.25f;

        [Tooltip("Seconds to ease the Climb Speed toward its target (SmoothDamp; ~0 = near-instant).")]
        [SerializeField] private float _smoothTime = 0.2f;

        private MaterialPropertyBlock _block;
        private float _baseSpeed;
        private float _currentSpeed;
        private float _speedVelocity;

        protected override void Awake()
        {
            base.Awake();
            _block = new MaterialPropertyBlock();

            // The artist-authored climb speed on the material is the ceiling; this only ever dampens toward
            // _outOfSightFactor of it, so it can't spin faster than the tuned look.
            var material = _spiralRenderer != null ? _spiralRenderer.sharedMaterial : null;
            _baseSpeed = material != null && material.HasProperty(SpeedId) ? material.GetFloat(SpeedId) : 0f;
        }

        protected override void OnSightTick(float centrality)
        {
            if (_spiralRenderer == null)
            {
                return;
            }

            var target = _baseSpeed * Mathf.Lerp(_outOfSightFactor, 1f, centrality);
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, target, ref _speedVelocity, _smoothTime);

            _spiralRenderer.GetPropertyBlock(_block);
            _block.SetFloat(SpeedId, _currentSpeed);
            _spiralRenderer.SetPropertyBlock(_block);
        }

        protected override void ResetReaction()
        {
            _currentSpeed = _baseSpeed * _outOfSightFactor;
            _speedVelocity = 0f;
        }
    }
}
