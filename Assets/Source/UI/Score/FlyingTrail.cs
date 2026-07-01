using System;
using BalloonParty.Shared.Pool;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    public class FlyingTrail : MonoBehaviour, IPoolable
    {
        private const string OverlaySortingLayer = "UI";
        private const int OverlaySortingOrder = 100;

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private AnimationCurve _scaleCurve;
        [SerializeField] private AnimationCurve _moveCurve;

        private Tweener _moveTween;

        private Func<Vector3> _followTarget;
        private Action _followArrived;
        private float _followSpeed;
        private float _arriveRadiusSqr;
        private bool _followUnscaled;
        private bool _following;

        private void Awake()
        {
            _renderer.sortingLayerName = OverlaySortingLayer;
            _trailRenderer.sortingLayerName = OverlaySortingLayer;
            ApplySortingOrder(OverlaySortingOrder);
        }

        private void Update()
        {
            if (!_following)
            {
                return;
            }

            var target = _followTarget();
            var dt = _followUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            var next = Vector3.MoveTowards(transform.position, target, _followSpeed * dt);
            transform.position = next;

            if ((next - target).sqrMagnitude <= _arriveRadiusSqr)
            {
                _following = false;
                var arrived = _followArrived;
                _followArrived = null;
                arrived?.Invoke();
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _moveTween = null;
            _following = false;
            _followTarget = null;
            _followArrived = null;
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
            bool useUnscaledTime = false)
        {
            ApplyColor(color);
            Setup(target, duration, onCompleted, useUnscaledTime);
        }

        public void Setup(
            Vector3 target,
            float duration,
            Action onCompleted,
            bool useUnscaledTime = false)
        {
            _trailRenderer.Clear();

            TraceTo(target, duration, useUnscaledTime);
            transform.DOScale(Vector3.zero, duration)
                .SetEase(_scaleCurve)
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
            bool useUnscaledTime = false)
        {
            ApplyColor(color);
            _trailRenderer.Clear();

            var totalDuration = burstDuration + traceDuration;
            transform.DOScale(Vector3.zero, totalDuration)
                .SetEase(_scaleCurve)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => onCompleted?.Invoke());

            _moveTween = transform.DOMove(burstTo, burstDuration)
                .SetUpdate(useUnscaledTime)
                .OnComplete(() => TraceTo(target, traceDuration, useUnscaledTime));
        }

        /// <summary>
        /// Homes on a live-updating target rather than a fixed point: each frame it moves toward
        /// <paramref name="targetProvider"/>() at <paramref name="speed"/>, firing <paramref name="onArrived"/>
        /// once it lands within <paramref name="arriveRadius"/>. Lets a trail chase a moving object (an
        /// overflow balloon still sliding as the pile compacts) and pop it exactly on contact.
        /// </summary>
        public void SetupFollow(
            Func<Vector3> targetProvider,
            float speed,
            float arriveRadius,
            Action onArrived,
            bool useUnscaledTime = false)
        {
            _trailRenderer.Clear();
            _followTarget = targetProvider;
            _followArrived = onArrived;
            _followSpeed = speed;
            _arriveRadiusSqr = arriveRadius * arriveRadius;
            _followUnscaled = useUnscaledTime;
            _following = true;
        }

        public void DisableMoveTween()
        {
            _moveTween?.Kill();
            _moveTween = null;
        }

        // The curved flight to a target that both the plain flight and the burst's second leg share.
        private void TraceTo(Vector3 target, float duration, bool useUnscaledTime)
        {
            _moveTween = transform.DOMove(target, duration).SetEase(_moveCurve).SetUpdate(useUnscaledTime);
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
    }
}
