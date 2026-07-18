using BalloonParty.Shared.Extensions;
using TMPro;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    [RequireComponent(typeof(TMP_Text))]
    public class ScoreCounterLabel : MonoBehaviour
    {
        private TMP_Text _label;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
        }

        public void Bind(IReadOnlyReactiveProperty<int> score)
        {
            score.Subscribe(OnScoreChanged).AddTo(this);
        }

        private void OnScoreChanged(int value)
        {
            _label.SetThousands(value);
        }
    }
}
