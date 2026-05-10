#region

using UniRx;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace BalloonParty.UI.Score
{
    [RequireComponent(typeof(Text))]
    public class ScoreCounterLabel : MonoBehaviour
    {
        private Text _label;

        private void Awake()
        {
            _label = GetComponent<Text>();
        }

        public void Bind(IReadOnlyReactiveProperty<int> score)
        {
            score.Subscribe(s => _label.text = s.ToString("N0"))
                .AddTo(this);
        }
    }
}
