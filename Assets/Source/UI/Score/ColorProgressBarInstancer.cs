using BalloonParty.Game;
using BalloonParty.Shared;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Score
{
    public class ColorProgressBarInstancer : MonoBehaviour
    {
        [Header("Prefabs")] [SerializeField] private ColorProgressBar _colorProgressBarPrefab;

        [Inject] private IGameConfiguration _config;
        [Inject] private IObjectResolver _resolver;
        [Inject] private ScoreController _scoreController;

        private void Start()
        {
            foreach (var color in _config.BalloonColors)
            {
                var bar = _resolver.Instantiate(_colorProgressBarPrefab, transform);
                bar.Setup(color, _scoreController);
            }
        }
    }
}
