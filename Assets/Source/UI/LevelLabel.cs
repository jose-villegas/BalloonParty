using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BalloonParty.Game;

namespace BalloonParty.UI
{
    [RequireComponent(typeof(Text))]
    public class LevelLabel : MonoBehaviour
    {
        [SerializeField] private bool _showNextLevel;

        [Inject] private ScoreController _scoreController;

        private Text _label;

        private void Awake() => _label = GetComponent<Text>();

        private void Start()
        {
            _scoreController.Level
                .Subscribe(level => _label.text = (level + (_showNextLevel ? 1 : 0)).ToString("N0"))
                .AddTo(this);
        }
    }
}

