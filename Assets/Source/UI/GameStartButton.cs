using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;

namespace BalloonParty.UI
{
    [RequireComponent(typeof(Button))]
    public class GameStartButton : MonoBehaviour
    {
        [Inject] private IPublisher<SpawnBalloonLineMessage> _spawnPublisher;
        [Inject] private IGameConfiguration _config;

        private Button _button;

        private void Start()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void OnDestroy() => _button.onClick.RemoveListener(OnClick);

        private void OnClick()
        {
            for (var i = 0; i < _config.GameStartedBalloonLines; i++)
                _spawnPublisher.Publish(default);

            gameObject.SetActive(false);
        }
    }
}

