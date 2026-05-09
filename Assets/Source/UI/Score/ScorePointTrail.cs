using System;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    public class ScorePointTrail : MonoBehaviour, IReusable
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private AnimationCurve _scaleCurve;
        [SerializeField] private AnimationCurve _moveCurve;

        public bool IsUsable { get; private set; } = true;

        public void Setup(Vector3 target, Color color, IGameConfiguration config, Action onCompleted)
        {
            IsUsable = false;

            _renderer.color = color;
            _trailRenderer.startColor = color;
            _trailRenderer.Clear();

            var moveTween = transform.DOMove(target, config.ScorePointTraceDuration);
            var scaleTween = transform.DOScale(Vector3.zero, config.ScorePointTraceDuration);
            scaleTween.SetEase(_scaleCurve);
            moveTween.SetEase(_moveCurve);

            scaleTween.OnComplete(() =>
            {
                IsUsable = true;
                onCompleted?.Invoke();
            });
        }
    }
}