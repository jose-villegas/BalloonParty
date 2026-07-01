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

        private void Awake()
        {
            _renderer.sortingLayerName = OverlaySortingLayer;
            _trailRenderer.sortingLayerName = OverlaySortingLayer;
            ApplySortingOrder(OverlaySortingOrder);
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _moveTween = null;
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
