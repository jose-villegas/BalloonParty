using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BalloonParty.Game;

namespace BalloonParty.UI
{
    [RequireComponent(typeof(Text))]
    public class ScoreCounterLabel : MonoBehaviour
    {
        [Inject] private ScoreController _scoreController;

        private Text _label;

        private void Awake() => _label = GetComponent<Text>();

        private void Start()
        {
            _scoreController.TotalScore
                .Subscribe(score => _label.text = score.ToString("N0"))
                .AddTo(this);
        }
    }
}

