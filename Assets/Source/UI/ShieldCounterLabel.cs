using MessagePipe;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;

namespace BalloonParty.UI
{
    [RequireComponent(typeof(Text))]
    public class ShieldCounterLabel : MonoBehaviour
    {
        [Inject] private ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        [Inject] private ISubscriber<BalanceBalloonsMessage> _balanceSubscriber;
        [Inject] private IGameConfiguration _config;

        private Text _label;
        private readonly CompositeDisposable _disposable = new();

        private void Awake() => _label = GetComponent<Text>();

        private void Start()
        {
            _label.text = "--";

            _loadedSubscriber
                .Subscribe(_ => _label.text = _config.ProjectileStartingShields.ToString("N0"))
                .AddTo(_disposable);

            _balanceSubscriber
                .Subscribe(_ => _label.text = "--")
                .AddTo(_disposable);
        }

        private void OnDestroy() => _disposable.Dispose();
    }
}

