using System;
using BalloonParty.Shared;
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

        private static readonly AnimationCurve LinearFallback = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private TrailRenderer _trailRenderer;

        // Per-TrailMotion style (curves + colour), indexed by the enum's ordinal (O(1)). Index 0
        // (Default) holds the base curves; a motion with a null curve — or one past the end of the array —
        // falls back to Default, then to a linear ease. Colour falls back to the caller's / prefab colour.
        [EnumIndexed(typeof(TrailMotion))]
        [SerializeField] private MotionStyle[] _motions;

        private Tweener _moveTween;

        private Func<Vector3> _followTarget;
        private Action _followArrived;
        private AnimationCurve _followCurve;
        private Vector3 _followStart;
        private float _followDuration;
        private float _followElapsed;
        private bool _followUnscaled;
        private bool _following;
        private Color _defaultColor;

        private void Awake()
        {
            _renderer.sortingLayerName = OverlaySortingLayer;
            _trailRenderer.sortingLayerName = OverlaySortingLayer;
            ApplySortingOrder(OverlaySortingOrder);
            _defaultColor = _renderer.color;
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

            // Curve-eased progress from the launch point to the target's *current* position, so the trail
            // follows the balloon as the pile compacts yet still lands on it exactly at t = 1.
            transform.position = Vector3.LerpUnclamped(_followStart, _followTarget(), _followCurve.Evaluate(t));

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
            // Reset to the prefab's authored colour on every fetch, so a pooled trail never inherits a
            // previous use's tint. Setup then layers an explicit or per-motion colour on top when asked.
            ApplyColor(_defaultColor);
        }

        public void OnDespawned()
        {
            _moveTween = null;
            _following = false;
            _followTarget = null;
            _followArrived = null;
            _followCurve = null;
            transform.DOKill();
            ApplySortingOrder(OverlaySortingOrder);
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
            ApplyMotionColor(motion);
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
            transform.DOScale(Vector3.zero, duration)
                .SetEase(ScaleCurveFor(motion))
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => onCompleted?.Invoke());
        }

        /// <summary>
        /// Two-phase flight: bloom out from current position to <paramref name="burstTo"/>,
        /// then follow the normal curve to <paramref name="target"/>.
        /// The scale tween spans the full journey so the orb shrinks continuously.
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
            ApplyColor(color);
            _trailRenderer.Clear();

            var totalDuration = burstDuration + traceDuration;
            transform.DOScale(Vector3.zero, totalDuration)
                .SetEase(ScaleCurveFor(motion))
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => onCompleted?.Invoke());

            _moveTween = transform.DOMove(burstTo, burstDuration)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => TraceTo(target, traceDuration, useUnscaledTime, motion));
        }

        /// <summary>
        /// Homes on a live-updating target rather than a fixed point: over <paramref name="duration"/> it
        /// eases from its launch position to <paramref name="targetProvider"/>()'s current value along the
        /// move curve for <paramref name="motion"/>, firing <paramref name="onArrived"/> at the end. Lets a
        /// trail chase a moving object (an overflow balloon still sliding as the pile compacts) and land on
        /// it exactly, with the same curve control as the fixed-point flights.
        /// </summary>
        public void SetupFollow(
            Func<Vector3> targetProvider,
            float duration,
            Action onArrived,
            bool useUnscaledTime = false,
            TrailMotion motion = TrailMotion.Default)
        {
            _trailRenderer.Clear();
            ApplyMotionColor(motion);
            _followTarget = targetProvider;
            _followArrived = onArrived;
            _followCurve = MoveCurveFor(motion);
            _followStart = transform.position;
            _followDuration = Mathf.Max(duration, Mathf.Epsilon);
            _followElapsed = 0f;
            _followUnscaled = useUnscaledTime;
            _following = true;
        }

        public void DisableMoveTween()
        {
            _moveTween?.Kill();
            _moveTween = null;
        }

        // The curved flight to a target that both the plain flight and the burst's second leg share.
        private void TraceTo(Vector3 target, float duration, bool useUnscaledTime, TrailMotion motion)
        {
            _moveTween = transform.DOMove(target, duration).SetEase(MoveCurveFor(motion)).SetUpdate(useUnscaledTime);
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

        // Tints the trail to the motion's colour only when that motion overrides it; otherwise leaves the
        // colour reset by OnSpawned (the prefab default) in place.
        private void ApplyMotionColor(TrailMotion motion)
        {
            var i = (int)motion;
            if (_motions != null && i >= 0 && i < _motions.Length && _motions[i].OverrideColor)
            {
                ApplyColor(_motions[i].Color);
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
            public AnimationCurve Move;
            public AnimationCurve Scale;
            public bool OverrideColor;

            // AllowNesting lets NaughtyAttributes evaluate ShowIf inside this nested (array-element) struct.
            [ShowIf(nameof(OverrideColor))]
            [AllowNesting]
            public Color Color;
        }
    }
}
