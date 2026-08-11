using BalloonParty.Projectile;
using UniRx;
using UnityEngine;

namespace BalloonParty.Prediction
{
    /// <summary>
    ///     Single per-actor producer of the "is the loaded shot's aim-prediction line sighted on this
    ///     actor, and how centrally" signal. Owns the polyline-vs-circle trace test — run once per frame,
    ///     skipped whenever neither the trace nor the target moved since the last evaluation — and
    ///     publishes it for any number of composed consumers: <see cref="TraceHitMarker"/> and item
    ///     reactions (see <see cref="BalloonParty.Item.SightReaction"/>), so none of them re-trace.
    ///     Pooled/non-DI: the host hands it an <see cref="IProjectileFacingSource"/> via
    ///     <see cref="Configure"/>.
    /// </summary>
    internal class PredictionSightProbe : MonoBehaviour
    {
        // 1cm — below this, the target is considered stationary since the last evaluation.
        private const float PositionEpsilonSqr = 0.0001f;

        [Tooltip("Point tested against the aim prediction trace. Defaults to this object if left unset.")]
        [SerializeField] private Transform _target;

        [Tooltip("World radius of the circle the prediction trace must pass within to count as sighted.")]
        [SerializeField] private float _hitRadius;

        [Tooltip("Centrality (0..1) at which IsSighted latches ON — the enter edge for one-shot reactions.")]
        [Range(0f, 1f)]
        [SerializeField] private float _sightEnter = 0.2f;

        [Tooltip("Centrality at which IsSighted latches OFF. Below the enter value so the edge can't chatter.")]
        [Range(0f, 1f)]
        [SerializeField] private float _sightExit = 0.05f;

        private readonly ReactiveProperty<float> _sight = new ReactiveProperty<float>(0f);
        private readonly ReactiveProperty<bool> _isSighted = new ReactiveProperty<bool>(false);

        private IProjectileFacingSource _source;
        private int _lastVersion = int.MinValue;
        private Vector3 _lastPosition;

        /// <summary>Raw centrality of the aim on the target this frame: 0 = not sighted, up to 1 = dead centre.</summary>
        internal IReadOnlyReactiveProperty<float> Sight => _sight;

        /// <summary>Hysteretic latch (enter/exit thresholds) — the enter/exit EDGE for one-shot reactions.</summary>
        internal IReadOnlyReactiveProperty<bool> IsSighted => _isSighted;

        /// <summary>Whether the trace crosses the target's circle this frame — the raw crossing test, distinct from <see cref="IsSighted"/>'s hysteretic latch.</summary>
        internal bool HasHit { get; private set; }

        /// <summary>Raw strike travel direction where the trace crossed this frame; zero when there's no crossing.</summary>
        internal Vector2 SightDirection { get; private set; }

        /// <summary>Raw world point where the trace crossed this frame; zero when there's no crossing.</summary>
        internal Vector2 SightPoint { get; private set; }

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
                ClearSight();
                return;
            }

            // Skip the trace test entirely when nothing that could change its answer has: the aim
            // polyline is unchanged (version) and this target hasn't moved past the epsilon — lets N
            // pooled probes (item reactions, balloon markers) share one evaluation cost per real change.
            var position = _target.position;
            if (_source.PredictionVersion == _lastVersion
                && (position - _lastPosition).sqrMagnitude < PositionEpsilonSqr)
            {
                return;
            }

            _lastVersion = _source.PredictionVersion;
            _lastPosition = position;

            var hit = TraceHitGeometry.TryFindSurfaceHit(
                _source.PredictionPoints, position, _hitRadius, out var point, out var centrality, out var direction);

            HasHit = hit;
            if (!hit)
            {
                ClearSight();
                return;
            }

            SightDirection = direction;
            SightPoint = point;
            _sight.Value = centrality;

            // Hysteresis: latch on above the enter threshold, off below exit — the gap keeps a graze at the
            // radius edge from chattering the enter/exit edge that one-shot reactions fire on.
            if (!_isSighted.Value && centrality >= _sightEnter)
            {
                _isSighted.Value = true;
            }
            else if (_isSighted.Value && centrality <= _sightExit)
            {
                _isSighted.Value = false;
            }
        }

        // Pooled reuse (the whole prefab GameObject toggled by PoolChannel<T>.Get/Return) must not carry a
        // stale cache into the next life — force a fresh evaluation next LateUpdate.
        private void OnDisable()
        {
            _lastVersion = int.MinValue;
            ClearSight();
        }

        private void OnDestroy()
        {
            _sight.Dispose();
            _isSighted.Dispose();
        }

        // Called by the host each time the icon is shown (the pool channel doesn't DI-inject). Null-safe:
        // clearing the source (or never setting one) just holds every signal at rest.
        internal void Configure(IProjectileFacingSource source)
        {
            _source = source;
        }

        private void ClearSight()
        {
            HasHit = false;
            _sight.Value = 0f;
            _isSighted.Value = false;
            SightDirection = Vector2.zero;
            SightPoint = Vector2.zero;
        }
    }
}
