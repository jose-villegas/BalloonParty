using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI.GameStart
{
    [RequireComponent(typeof(Button))]
    public class GameStartButton : MonoBehaviour
    {
        private Button _button;
        [Inject] private IGameConfiguration _config;
        [Inject] private IPublisher<SpawnBalloonLineMessage> _spawnPublisher;

        private void Start()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            _spawnPublisher.Publish(new SpawnBalloonLineMessage(_config.GameStartedBalloonLines));
            gameObject.SetActive(false);
        }
    }
}