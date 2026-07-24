using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Dampens a PierceConeSpiral renderer's Climb Speed (<c>_Speed</c>) when the loaded shot's aim
    ///     prediction line is NOT sighted on this item, read from a <see cref="PredictionSightProbe"/>'s
    ///     listenable <see cref="PredictionSightProbe.Sight"/> signal. Sighted (aim dead-centre) runs at the
    ///     material's authored speed; off-aim it eases down to a fraction of it — so the drill idles slow and
    ///     spins up to its normal rate as the aim centres, never faster. One of potentially several
    ///     behaviours off the same probe; lives on a pooled item visual, so it isn't DI-injected.
    /// </summary>
    internal class PredictionSightSpiral : MonoBehaviour
    {
        private static readonly int SpeedId = Shader.PropertyToID("_Speed");

        [Tooltip("Sight source. Defaults to a PredictionSightProbe on this object or a parent if left unset.")]
        [SerializeField] private PredictionSightProbe _sightProbe;

        [Tooltip("Renderer carrying the PierceConeSpiral material whose Climb Speed (_Speed) is driven.")]
        [SerializeField] private Renderer _spiralRenderer;

        [Tooltip("Fraction of the material's authored Climb Speed used when the shot isn't lined up through " +
                 "this item (0 = frozen, 1 = no dampening). Sighted always runs at the full authored speed.")]
        [Range(0f, 1f)]
        [SerializeField] private float _outOfSightFactor = 0.25f;

        [Tooltip("Seconds to ease the Climb Speed toward its target (SmoothDamp; ~0 = near-instant).")]
        [SerializeField] private float _smoothTime = 0.2f;

        private MaterialPropertyBlock _block;
        private float _baseSpeed;
        private float _currentSpeed;
        private float _speedVelocity;

        private void Awake()
        {
            if (_sightProbe == null)
            {
                _sightProbe = GetComponentInParent<PredictionSightProbe>();
            }

            _block = new MaterialPropertyBlock();

            // The artist-authored climb speed on the material is the ceiling; this component only ever
            // dampens toward _outOfSightFactor of it, so it can't spin faster than the tuned look.
            var material = _spiralRenderer != null ? _spiralRenderer.sharedMaterial : null;
            _baseSpeed = material != null && material.HasProperty(SpeedId) ? material.GetFloat(SpeedId) : 0f;
        }

        // Re-armed on every pooled reactivation so a reused icon starts at the idle (out-of-sight) speed
        // rather than whatever the previous host left mid-ramp.
        private void OnEnable()
        {
            _currentSpeed = _baseSpeed * _outOfSightFactor;
            _speedVelocity = 0f;
        }

        // Polls the probe's .Value (a one-frame lag behind its LateUpdate is invisible through the
        // SmoothDamp) rather than subscribing, because the ease runs every frame regardless.
        private void LateUpdate()
        {
            if (_spiralRenderer == null || _sightProbe == null)
            {
                return;
            }

            var target = _baseSpeed * Mathf.Lerp(_outOfSightFactor, 1f, _sightProbe.Sight.Value);
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, target, ref _speedVelocity, _smoothTime);

            _spiralRenderer.GetPropertyBlock(_block);
            _block.SetFloat(SpeedId, _currentSpeed);
            _spiralRenderer.SetPropertyBlock(_block);
        }
    }
}
