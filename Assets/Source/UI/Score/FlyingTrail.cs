using System;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using DG.Tweening;
using NaughtyAttributes;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    public class FlyingTrail : MonoBehaviour, IPoolable
    {
        private const string OverlaySortingLayer = "UI";
        private const int OverlaySortingOrder = 100;

        // A ribbon lifetime long enough to outlast any level-up freeze, so a paused orb's tail doesn't decay
        // while it hangs behind the popup. Mirrors ShapeFormationTicker's formation-vertex freeze.
        private const float FrozenRibbonTime = 600f;

        private static readonly AnimationCurve LinearFallback = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private TrailRenderer _trailRenderer;

        // Indexed by TrailMotion ordinal; missing/out-of-range entries fall back to index 0, then linear 0→1.
        [EnumIndexed(typeof(TrailMotion))]
        [SerializeField] private MotionStyle[] _motions;

        private Tweener _moveTween;

        private Func<Vector3> _followTarget;
        private Action _followArrived;
        private AnimationCurve _followCurve;
        private AnimationCurve _followScaleCurve;
        private Vector3 _followStart;
        private float _followDuration;
        private float _followElapsed;
        private bool _followUnscaled;
        private bool _following;
        private Gradient _flightGradient;
        // Shared scratch for TransformRibbon — main-thread only, grown on demand, never shrunk.
        private static Vector3[] _ribbonScratch = new Vector3[256];

        private Color _defaultColor;
        private Vector3 _defaultScale;
        private float _defaultRibbonTime;

        private void Awake()
        {
            _renderer.sortingLayerName = OverlaySortingLayer;
            _trailRenderer.sortingLayerName = OverlaySortingLayer;
            ApplySortingOrder(OverlaySortingOrder);
            _defaultColor = _renderer.color;
            _defaultScale = transform.localScale;
            _defaultRibbonTime = _trailRenderer.time;
        }

        private void Update()
        {
            if (!_following)
            {
                return;
            }

            var dt = _followUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            _followElapsed += dt;
            var t = Mathf.Clamp01(_followElapsed / _followDuration);

            // Re-evaluates target() each frame so a moving target is still hit exactly at t = 1.
            transform.position = Vector3.LerpUnclamped(_followStart, _followTarget(), _followCurve.Evaluate(t));
            transform.localScale = _defaultScale * _followScaleCurve.Evaluate(t);
            SampleFlightColor(t);

            if (t >= 1f)
            {
                _following = false;
                var arrived = _followArrived;
                _followArrived = null;
                arrived?.Invoke();
            }
        }

        public void OnSpawned()
        {
            // Reset to authored colour so a pooled trail never inherits a previous tint.
            ApplyColor(_defaultColor);
        }

        public void OnDespawned()
        {
            _moveTween = null;
            _following = false;
            _followTarget = null;
            _followArrived = null;
            _followCurve = null;
            _followScaleCurve = null;
            _flightGradient = null;
            transform.DOKill();
            ApplySortingOrder(OverlaySortingOrder);
            // Restore the authored ribbon length/emission so a formation's overrides can't leak into pooled reuse.
            _trailRenderer.time = _defaultRibbonTime;
            _trailRenderer.emitting = true;
        }

        public void SetSortingOrder(int order)
        {
            ApplySortingOrder(order);
        }

        public void Setup(
            Vector3 target,
            Color color,
            float duration,
            Action onCompleted,
            bool useUnscaledTime = false,
            TrailMotion motion = TrailMotion.Default)
        {
            _flightGradient = null;
            ApplyColor(color);
            SetupTrace(target, duration, onCompleted, useUnscaledTime, motion);
        }

        public void Setup(
            Vector3 target,
            float duration,
            Action onCompleted,
            bool useUnscaledTime = false,
            TrailMotion motion = TrailMotion.Default)
        {
            BeginMotionGradient(motion);
            SetupTrace(target, duration, onCompleted, useUnscaledTime, motion);
        }

        private void SetupTrace(
            Vector3 target,
            float duration,
            Action onCompleted,
            bool useUnscaledTime,
            TrailMotion motion)
        {
            _trailRenderer.Clear();

            TraceTo(target, duration, useUnscaledTime, motion);
            AnimateOverFlight(duration, useUnscaledTime, motion, onCompleted);
        }

        /// <summary>
        /// Two-phase flight: bloom to <paramref name="burstTo"/>, then curve to <paramref name="target"/>.
        /// </summary>
        public void SetupBurst(
            Vector3 burstTo,
            Vector3 target,
            Color color,
            float burstDuration,
            float traceDuration,
            Action onCompleted,
            bool useUnscaledTime = false,
            TrailMotion motion = TrailMotion.Default)
        {
            _flightGradient = null;
            ApplyColor(color);
            _trailRenderer.Clear();

            var totalDuration = burstDuration + traceDuration;
            AnimateOverFlight(totalDuration, useUnscaledTime, motion, onCompleted);

            _moveTween = transform.DOMove(burstTo, burstDuration)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => TraceTo(target, traceDuration, useUnscaledTime, motion));
        }

        /// <summary>
        /// Homes on a live-updating target instead of a fixed point, firing <paramref name="onArrived"/> at the end.
        /// </summary>
        public void SetupFollow(
            Func<Vector3> targetProvider,
            float duration,
            Action onArrived,
            bool useUnscaledTime = false,
            TrailMotion motion = TrailMotion.Default)
        {
            _trailRenderer.Clear();
            BeginMotionGradient(motion);
            _followTarget = targetProvider;
            _followArrived = onArrived;
            _followCurve = MoveCurveFor(motion);
            _followScaleCurve = ScaleCurveFor(motion);
            _followStart = transform.position;
            _followDuration = Mathf.Max(duration, Mathf.Epsilon);
            _followElapsed = 0f;
            _followUnscaled = useUnscaledTime;
            _following = true;
            transform.localScale = _defaultScale * _followScaleCurve.Evaluate(0f);
        }

        public void DisableMoveTween()
        {
            LifecycleHelper.KillAndClear(ref _moveTween);
        }

        // Solid tint with no flight gradient — for trails a caller drives directly (formation vertices/carrier)
        // rather than through Setup, which would otherwise apply the colour for it.
        internal void SetColor(Color color)
        {
            _flightGradient = null;
            ApplyColor(color);
        }

        // Per-tier ribbon length so nested-star tiers stay visible through the whole sequence; OnDespawned
        // restores the authored value.
        internal void SetRibbonTime(float time)
        {
            _trailRenderer.time = time;
        }

        // Called when a flight is paused (level-up freeze): inflate the ribbon lifetime so the frozen orb keeps
        // its tail. OnDespawned (on eventual completion) restores the authored value, and ThawRibbon covers a
        // resume. Formation anchors have no FlyingTrail, so only tween-driven default trails hit this.
        internal void FreezeRibbon()
        {
            _trailRenderer.time = FrozenRibbonTime;
        }

        internal void ThawRibbon()
        {
            _trailRenderer.time = _defaultRibbonTime;
        }

        // Pen up/down: formations travel between vertices without drawing (deploy), then emit while
        // actually tracing chords — otherwise the deploy spokes bury the drawn shape.
        internal void SetRibbonEmitting(bool emitting)
        {
            _trailRenderer.emitting = emitting;
        }

        // Rigidly re-frames the recorded ribbon by the formation's delta transform (translate + 3D rotate).
        // Ribbons record WORLD positions, so a formation that glides or tumbles while it draws would shear
        // every line; carrying each drawn point through p' = newCenter + delta·(p − oldCenter) keeps the
        // whole figure rigid in formation space while its centre glides and its frame tumbles.
        internal void TransformRibbon(Vector3 oldCenter, Vector3 newCenter, Quaternion delta)
        {
            var count = _trailRenderer.positionCount;
            if (count == 0)
            {
                return;
            }

            if (_ribbonScratch.Length < count)
            {
                _ribbonScratch = new Vector3[Mathf.NextPowerOfTwo(count)];
            }

            _trailRenderer.GetPositions(_ribbonScratch);

            // Pure translation is the common case (the shape holds a fixed tilt until its draw finishes) —
            // skip the quaternion rotate entirely.
            if (delta == Quaternion.identity)
            {
                var offset = newCenter - oldCenter;

                // Per-index writes: SetPositions takes its count from the ARRAY length, which would push
                // scratch garbage past the ribbon's real point count.
                for (var i = 0; i < count; i++)
                {
                    _trailRenderer.SetPosition(i, _ribbonScratch[i] + offset);
                }

                return;
            }

            for (var i = 0; i < count; i++)
            {
                _trailRenderer.SetPosition(i, newCenter + delta * (_ribbonScratch[i] - oldCenter));
            }
        }

        internal void ClearRibbon()
        {
            _trailRenderer.Clear();
        }

        // Shared by the plain flight and the burst's second leg.
        private void TraceTo(Vector3 target, float duration, bool useUnscaledTime, TrailMotion motion)
        {
            _moveTween = transform.DOMove(target, duration).SetEase(MoveCurveFor(motion)).SetUpdate(useUnscaledTime);
        }

        // Drives scale and colour over normalized flight time; fires onCompleted when duration elapses.
        private void AnimateOverFlight(float duration, bool useUnscaledTime, TrailMotion motion, Action onCompleted)
        {
            var curve = ScaleCurveFor(motion);
            transform.localScale = _defaultScale * curve.Evaluate(0f);
            DOTween.To(
                    () => 0f,
                    t =>
                    {
                        transform.localScale = _defaultScale * curve.Evaluate(t);
                        SampleFlightColor(t);
                    },
                    1f,
                    duration)
                .SetEase(Ease.Linear)
                .SetTarget(transform)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => onCompleted?.Invoke());
        }

        private AnimationCurve MoveCurveFor(TrailMotion motion)
        {
            var i = (int)motion;
            if (_motions != null)
            {
                if (i >= 0 && i < _motions.Length && _motions[i].Move != null)
                {
                    return _motions[i].Move;
                }

                if (_motions.Length > 0 && _motions[0].Move != null)
                {
                    return _motions[0].Move;
                }
            }

            return LinearFallback;
        }

        private AnimationCurve ScaleCurveFor(TrailMotion motion)
        {
            var i = (int)motion;
            if (_motions != null)
            {
                if (i >= 0 && i < _motions.Length && _motions[i].Scale != null)
                {
                    return _motions[i].Scale;
                }

                if (_motions.Length > 0 && _motions[0].Scale != null)
                {
                    return _motions[0].Scale;
                }
            }

            return LinearFallback;
        }

        // A motion without an override leaves the OnSpawned default colour in place.
        private void BeginMotionGradient(TrailMotion motion)
        {
            var i = (int)motion;
            _flightGradient = _motions != null && i >= 0 && i < _motions.Length
                              && _motions[i].OverrideColor && _motions[i].Gradient != null
                ? _motions[i].Gradient
                : null;

            SampleFlightColor(0f);
        }

        private void SampleFlightColor(float t)
        {
            if (_flightGradient != null)
            {
                ApplyColor(_flightGradient.Evaluate(t));
            }
        }

        private void ApplyColor(Color color)
        {
            _renderer.color = color;
            _trailRenderer.startColor = color;
        }

        private void ApplySortingOrder(int order)
        {
            _renderer.sortingOrder = order;
            _trailRenderer.sortingOrder = order;
        }

        [Serializable]
        private struct MotionStyle
        {
            // Path easing over normalized time (0,0 → 1,1 = constant speed).
            public AnimationCurve Move;

            // Scale as a factor of base scale over normalized time (1 = authored size, 0 = gone).
            public AnimationCurve Scale;

            public bool OverrideColor;

            // AllowNesting lets NaughtyAttributes evaluate ShowIf inside this array-element struct.
            [ShowIf(nameof(OverrideColor))]
            [AllowNesting]
            public Gradient Gradient;
        }
    }
}
