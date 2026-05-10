#region

using System;
using BalloonParty.Shared;
using DG.Tweening;
using UnityEngine;

#endregion

namespace BalloonParty.UI.Score
{
    public class ScorePointTrail : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private AnimationCurve _scaleCurve;
        [SerializeField] private AnimationCurve _moveCurve;

        private Action<ScorePointTrail> _returnToPool;

        public void OnSpawned() { }

        public void OnDespawned() { }

        public void Initialize(Action<ScorePointTrail> returnToPool)
        {
            _returnToPool = returnToPool;
        }

        public void Setup(Vector3 target, Color color, IGameConfiguration config, Action onCompleted)
        {
            _renderer.color = color;
            _trailRenderer.startColor = color;
            _trailRenderer.Clear();

            var moveTween = transform.DOMove(target, config.ScorePointTraceDuration);
            var scaleTween = transform.DOScale(Vector3.zero, config.ScorePointTraceDuration);
            scaleTween.SetEase(_scaleCurve);
            moveTween.SetEase(_moveCurve);

            scaleTween.OnComplete(() =>
            {
                onCompleted?.Invoke();
                _returnToPool?.Invoke(this);
            });
        }
    }
}
