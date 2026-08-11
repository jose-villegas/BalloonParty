using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     A <see cref="SightRampReaction"/> that points a GlitterSwirl material's <c>_Drift</c> along the
    ///     incoming shot — the specks stream in the direction the aim is travelling as it crosses this
    ///     item, rather than the material's fixed authored drift. Reads
    ///     <see cref="BalloonParty.Prediction.PredictionSightProbe.SightDirection" /> directly (not the
    ///     centrality <see cref="SightRampReaction" /> otherwise passes to <c>OnSightTick</c>) via the
    ///     shared <see cref="SightReaction.Probe" />. Writes via a <see cref="MaterialPropertyBlock" />
    ///     (mirrors <see cref="PredictionSightSpiral" />), so the shared material asset stays untouched.
    /// </summary>
    internal sealed class SightGlitterDriftReaction : SightRampReaction
    {
        private static readonly int DriftId = Shader.PropertyToID("_Drift");

        [Tooltip("Renderer carrying the GlitterSwirl material whose _Drift is driven.")]
        [SerializeField] private Renderer _glitterRenderer;

        [Tooltip("Drift speed at full strength (UV units/second) — tunes how fast the specks stream, " +
                 "independent of the aim's own speed.")]
        [SerializeField] private float _driftSpeed = 0.3f;

        [Tooltip("Seconds to ease the drift direction toward the incoming heading (SmoothDampAngle).")]
        [SerializeField] private float _smoothTime = 0.15f;

        private MaterialPropertyBlock _block;
        private float _currentAngleDeg;
        private float _angleVelocity;

        protected override void Awake()
        {
            base.Awake();
            _block = new MaterialPropertyBlock();
        }

        protected override void OnSightTick(float centrality)
        {
            if (_glitterRenderer == null || Probe == null)
            {
                return;
            }

            // No crossing this frame reports a zero SightDirection — hold the last heading instead of
            // snapping toward a degenerate direction (the specks just keep streaming the way they were).
            var direction = Probe.SightDirection;
            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                var targetAngleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                _currentAngleDeg = Mathf.SmoothDampAngle(_currentAngleDeg, targetAngleDeg, ref _angleVelocity, _smoothTime);
            }

            var rad = _currentAngleDeg * Mathf.Deg2Rad;
            var drift = new Vector4(Mathf.Cos(rad), Mathf.Sin(rad), 0f, 0f) * _driftSpeed;

            _glitterRenderer.GetPropertyBlock(_block);
            _block.SetVector(DriftId, drift);
            _glitterRenderer.SetPropertyBlock(_block);
        }

        protected override void ResetReaction()
        {
            _currentAngleDeg = 0f;
            _angleVelocity = 0f;
        }
    }
}
