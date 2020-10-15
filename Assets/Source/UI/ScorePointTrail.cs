using System;
using DG.Tweening;
using UnityEngine;

public class ScorePointTrail : MonoBehaviour, IReusable
{
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private TrailRenderer _trailRenderer;
    [SerializeField] private AnimationCurve _scaleCurve;
    [SerializeField] private AnimationCurve _moveCurve;

    public void Setup(Vector3 target, Color color, IGameConfiguration configuration, Action onCompleted)
    {
        IsUsable = false;

        _renderer.color = color;
        _trailRenderer.startColor = color;
        _trailRenderer.Clear();

        var moveTween = transform.DOMove(target, configuration.ScorePointTraceDuration);
        var scaleTween = transform.DOScale(Vector3.zero, configuration.ScorePointTraceDuration);
        scaleTween.SetEase(_scaleCurve);
        moveTween.SetEase(_moveCurve);

        scaleTween.onComplete += () =>
        {
            IsUsable = true;
            onCompleted?.Invoke();
        };
    }

    public bool IsUsable { get; private set; }
}