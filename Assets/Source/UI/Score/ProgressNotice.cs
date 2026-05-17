using System;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    public class ProgressNotice : MonoBehaviour, IPoolable
    {
        private static readonly int ScoreTrigger = Animator.StringToHash("Score");
        private static readonly int ScoreDisappearState = Animator.StringToHash("ScoreDisappear");
        [SerializeField] private ColorableRenderer[] _colorableRenderer;
        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _labelTransform;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private AnimationCurve _scaleCurve;
        [SerializeField] private AnimationCurve _labelOffsetXCurve;

        private Action _onComplete;
        private Transform _parent;

        public bool IsFullyShown { get; private set; }


        public void OnSpawned()
        {
            IsFullyShown = false;
            _onComplete = null;

            if (_parent != null)
            {
                transform.SetParent(_parent, false);
            }
        }

        public void OnDespawned() { }

        public void SetAnchoredPosition(Vector2 position)
        {
            var rect = (RectTransform)transform;
            rect.anchoredPosition = position;
        }

        public void SetParent(Transform parent)
        {
            _parent = parent;
            transform.SetParent(parent, false);
        }

        public void Show(int score, Action onComplete, Color? color = null)
        {
            _onComplete = onComplete;

            if (color.HasValue && _colorableRenderer != null && _colorableRenderer.Length > 0)
            {
                foreach (var colorable in _colorableRenderer)
                {
                    colorable.SetColor(color.Value);
                }
            }

            _animator.SetTrigger(ScoreTrigger);
            _label.text = score.ToString("N0");


            transform.localScale = Vector3.one;
            _labelTransform.localScale = Vector3.one * _scaleCurve.Evaluate(score);

            var labelRect = (RectTransform)_labelTransform;
            labelRect.anchoredPosition = new Vector2(_labelOffsetXCurve.Evaluate(score), labelRect.anchoredPosition.y);
        }

        public void Dismiss()
        {
            _animator.Play(ScoreDisappearState);
        }

        private void OnAnimationScoreFully()
        {
            IsFullyShown = true;
        }

        private void OnAnimationCompleted()
        {
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }
    }
}
