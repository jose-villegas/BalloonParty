using BalloonParty.Shared.Animation;
using TMPro;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    [RequireComponent(typeof(TMP_Text))]
    [RequireComponent(typeof(RollingTextAnimator))]
    public class ScoreCounterLabel : MonoBehaviour
    {
        private RollingTextAnimator _animator;

        private void Awake()
        {
            _animator = GetComponent<RollingTextAnimator>();
        }

        public void Bind(IReadOnlyReactiveProperty<int> score)
        {
            score.Subscribe(OnScoreChanged).AddTo(this);
        }

        private void OnScoreChanged(int value)
        {
            _animator.SetThousands(value);
        }
    }
}
