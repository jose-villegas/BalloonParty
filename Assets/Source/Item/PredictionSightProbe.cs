using BalloonParty.Prediction;
using BalloonParty.Projectile;
using UniRx;
using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Publishes how centrally the loaded shot's aim prediction line is sighted on a target point as a
    ///     listenable signal: 0 = the aim isn't passing through it (nothing aiming, or the trace misses the
    ///     radius), up to 1 = dead centre. Standalone — it owns the polyline-vs-circle trace test itself, so
    ///     any behaviour can subscribe to <see cref="Sight"/> without depending on
    ///     <see cref="ProjectileFacingRotator"/> (which runs its own copy for alignment). Lives on a pooled
    ///     item visual, so it isn't DI-injected: the host (ItemDisplayService) hands it an
    ///     <see cref="IProjectileFacingSource"/> via <see cref="Configure"/>, mirroring the rotator.
    /// </summary>
    internal class PredictionSightProbe : MonoBehaviour
    {
        [Tooltip("Point tested against the aim prediction trace. Defaults to this object if left unset.")]
        [SerializeField] private Transform _target;

        [Tooltip("World radius of the circle the prediction trace must pass within to count as sighted.")]
        [SerializeField] private float _hitRadius;

        private readonly ReactiveProperty<float> _sight = new ReactiveProperty<float>(0f);

        private IProjectileFacingSource _source;

        /// <summary>
        ///     Centrality of the aim on the target this frame: 0 = not sighted, up to 1 = dead centre.
        ///     Listenable (subscribe) or pollable (<c>.Value</c>).
        /// </summary>
        internal IReadOnlyReactiveProperty<float> Sight => _sight;

        private void Awake()
        {
            if (_target == null)
            {
                _target = transform;
            }
        }

        private void LateUpdate()
        {
            if (_source == null || !_source.IsAiming)
            {
                _sight.Value = 0f;
                return;
            }

            var sighted = TraceHitGeometry.TryFindSurfaceHit(
                _source.PredictionPoints, _target.position, _hitRadius, out _, out var centrality, out _);
            _sight.Value = sighted ? centrality : 0f;
        }

        private void OnDestroy()
        {
            _sight.Dispose();
        }

        // Called by the host each time the icon is shown (the pool channel doesn't DI-inject). Null-safe:
        // clearing the source (or never setting one) just holds the sight at 0.
        internal void Configure(IProjectileFacingSource source)
        {
            _source = source;
        }
    }
}
