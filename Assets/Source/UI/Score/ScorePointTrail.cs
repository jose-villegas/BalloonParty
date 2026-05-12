using System;
using BalloonParty.Shared.Pool;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    public class ScorePointTrail : MonoBehaviour, IPoolable
    {
        private const string OverlaySortingLayer = "UI";
        private const int OverlaySortingOrder = 100;

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private AnimationCurve _scaleCurve;
        [SerializeField] private AnimationCurve _moveCurve;

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
        }

        public void Setup(Vector3 target, Color color, float duration, Action onCompleted)
        {
            _renderer.color = color;
            _trailRenderer.startColor = color;
            Setup(target, duration, onCompleted);
        }

        public void Setup(Vector3 target, float duration, Action onCompleted)
        {
            _trailRenderer.Clear();

            var moveTween = transform.DOMove(target, duration);
            var scaleTween = transform.DOScale(Vector3.zero, duration);
            scaleTween.SetEase(_scaleCurve);
            moveTween.SetEase(_moveCurve);

            scaleTween.OnComplete(() => onCompleted?.Invoke());
        }
    }
}
