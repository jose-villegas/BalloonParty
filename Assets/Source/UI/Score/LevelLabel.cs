using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.UI.Score
{
    [RequireComponent(typeof(Text))]
    public class LevelLabel : MonoBehaviour
    {
        [SerializeField] private bool _showNextLevel;

        private Text _label;

        private void Awake() => _label = GetComponent<Text>();

        public void Bind(IReadOnlyReactiveProperty<int> level)
        {
            level.Subscribe(l => _label.text = (l + (_showNextLevel ? 1 : 0)).ToString("N0"))
                 .AddTo(this);
        }
    }
}
