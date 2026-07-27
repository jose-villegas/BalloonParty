using BalloonParty.Game.Run;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.GameOver
{
    /// <summary>
    ///     Placeholder loss screen; visibility is driven by a <see cref="CanvasGroup"/> so the
    ///     GameObject can stay active and keep its subscriptions alive (never toggle with SetActive).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GameOverScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _finalLevelLabel;
        [SerializeField] private TMP_Text _finalScoreLabel;
        [SerializeField] private TMP_Text _bestLevelLabel;
        [SerializeField] private TMP_Text _bestScoreLabel;
        [SerializeField] private UnityEngine.UI.Button _retryButton;
        [SerializeField] private TMP_Text _retryLabel;

        [Inject] private ISubscriber<GameOverMessage> _gameOverSubscriber;
        [Inject] private IRunMeta _runMeta;
        [Inject] private IRetryState _retryState;
        [Inject] private RetryTracker _retryTracker;
        [Inject] private IPublisher<GameOverDismissedMessage> _dismissedPublisher;
        [Inject] private GameOverPresentationGate _presentationGate;

        private CanvasGroup _canvasGroup;
        private FormattedLabel _finalLevel;
        private FormattedLabel _finalScore;
        private FormattedLabel _bestLevel;
        private FormattedLabel _bestScore;
        private int _deathLevel;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            // Wrap before the label text is overwritten so FormattedLabel captures the template.
            _finalLevel = new FormattedLabel(_finalLevelLabel);
            _finalScore = new FormattedLabel(_finalScoreLabel);
            _bestLevel = new FormattedLabel(_bestLevelLabel);
            _bestScore = new FormattedLabel(_bestScoreLabel);

            SetVisible(false);
        }

        private void Start()
        {
            _gameOverSubscriber.Subscribe(OnGameOver).AddTo(this);

            // Hide whenever we leave GameOver.
            Navigation.Current
                .Where(state => state != NavigationState.GameOver)
                .Subscribe(_ => SetVisible(false))
                .AddTo(this);
        }

        // Wired to the Restart button's onClick. Hides now; the restore cinematic then plays and
        // GameOverLossCinematic restarts the run once it ends — so the restart is deferred, not immediate.
        public void OnRestartPressed()
        {
            SetVisible(false);
            _dismissedPublisher.Publish(new GameOverDismissedMessage());
        }

        // Wired to the Try Again button's onClick. Prepares a retry at the death level then dismisses.
        public void OnRetryPressed()
        {
            _retryTracker.PrepareRetry(_deathLevel);
            SetVisible(false);
            _dismissedPublisher.Publish(new GameOverDismissedMessage());
        }

        // Labels are filled now; the reveal waits for the loss cinematic to open the gate.
        private void OnGameOver(GameOverMessage msg)
        {
            _deathLevel = msg.FinalLevel;
            _finalLevel.Set(msg.FinalLevel);
            _finalScore.Set(msg.FinalScore);
            _bestLevel.Set(_runMeta.BestLevel.Value);
            _bestScore.Set(_runMeta.BestScore.Value);
            SetRetryButtonVisible(_retryState.RetriesRemaining > 0);
            RevealAfterGateAsync().Forget();
        }

        private async UniTaskVoid RevealAfterGateAsync()
        {
            await _presentationGate.WaitAsync(destroyCancellationToken);
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private void SetRetryButtonVisible(bool visible)
        {
            if (_retryButton == null)
            {
                return;
            }

            _retryButton.interactable = visible;

            if (_retryLabel != null)
            {
                _retryLabel.text = $"Retry ({_retryState.RetriesRemaining})";
            }
        }
    }
}
