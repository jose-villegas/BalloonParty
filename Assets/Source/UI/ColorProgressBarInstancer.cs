using UnityEngine;
using VContainer;
using BalloonParty.Game;

namespace BalloonParty.UI
{
    public class ColorProgressBarInstancer : MonoBehaviour
    {
        [SerializeField] private ColorProgressBar _colorProgressBarPrefab;

        [Inject] private IGameConfiguration _config;
        [Inject] private IObjectResolver _resolver;
        [Inject] private ScoreController _scoreController;

        private void Start()
        {
            foreach (var color in _config.BalloonColors)
            {
                var bar = Object.Instantiate(_colorProgressBarPrefab, transform);
                _resolver.Inject(bar);
                bar.Setup(color, _scoreController);
            }
        }
    }
}


