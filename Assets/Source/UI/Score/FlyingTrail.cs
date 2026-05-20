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
            _renderer.sortingOrder = OverlaySortingOrder;
            _trailRenderer.sortingLayerName = OverlaySortingLayer;
            _trailRenderer.sortingOrder = OverlaySortingOrder;
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _moveTween = null;
            transform.DOKill();
        }

        public void Setup(
            Vector3 target,
            Color color,
            float duration,
            Action onCompleted,
            bool useUnscaledTime = false)
        {
            _renderer.color = color;
            _trailRenderer.startColor = color;
            Setup(target, duration, onCompleted, useUnscaledTime);
        }

        public void Setup(
            Vector3 target,
            float duration,
            Action onCompleted,
            bool useUnscaledTime = false)
        {
            _trailRenderer.Clear();

            _moveTween = transform.DOMove(target, duration);
            var scaleTween = transform.DOScale(Vector3.zero, duration);
            scaleTween.SetEase(_scaleCurve);
            _moveTween.SetEase(_moveCurve);

            if (useUnscaledTime)
            {
                _moveTween.SetUpdate(true);
                scaleTween.SetUpdate(true);
            }

            scaleTween.OnComplete(() => onCompleted?.Invoke());
        }

        public void DisableMoveTween()
        {
            _moveTween?.Kill();
            _moveTween = null;
        }
    }
}
