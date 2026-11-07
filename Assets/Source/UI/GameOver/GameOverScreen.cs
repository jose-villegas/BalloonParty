using System.Threading;
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

        [Tooltip("Retries with a bonus card for that attempt, bought with a rewarded ad. Hidden while no bonus " +
            "card can be dealt. Optional.")]
        [SerializeField] private UnityEngine.UI.Button _bonusRetryButton;

        [Inject] private ISubscriber<GameOverMessage> _gameOverSubscriber;
        [Inject] private IRunMeta _runMeta;
        [Inject] private IFailureOffers _failureOffers;
        [Inject] private IPublisher<GameOverDismissedMessage> _dismissedPublisher;
        [Inject] private GameOverPresentationGate _presentationGate;

        private CanvasGroup _canvasGroup;
        private FormattedLabel _finalLevel;
        private FormattedLabel _finalScore;
        private FormattedLabel _bestLevel;
        private FormattedLabel _bestScore;
        private int _deathLevel;
        private bool _claimingRetry;

        // One per shown offer: cancelled when the screen dismisses or navigation leaves GameOver, so an ad
        // still playing can't stage a retry for a screen that's gone.
        private CancellationTokenSource _offerCts;

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
                .Subscribe(_ =>
                {
                    CancelOffer();
                    SetVisible(false);
                })
                .AddTo(this);
        }

        private void OnDestroy()
        {
            CancelOffer();
        }

        // Wired to the Restart button's onClick. Hides now; the restore cinematic then plays and
        // GameOverLossCinematic restarts the run once it ends — so the restart is deferred, not immediate.
        public void OnRestartPressed()
        {
            // A retry being claimed owns the screen: a second dismiss would restart mid-cinematic.
            if (_claimingRetry)
            {
                return;
            }

            Dismiss();
        }

        // Wired to the Try Again button's onClick. IFailureOffers settles the retry's cost (an ad once the
        // free retries are used) and stages it; the screen then dismisses like Restart.
        public void OnRetryPressed()
        {
            if (_claimingRetry || _offerCts == null)
            {
                return;
            }

            RetryAsync(withBonusCard: false, _offerCts.Token).Forget();
        }

        // Wired to the bonus retry button's onClick: the rewarded ad, then the retry with a bonus card.
        public void OnBonusRetryPressed()
        {
            if (_claimingRetry || _offerCts == null)
            {
                return;
            }

            RetryAsync(withBonusCard: true, _offerCts.Token).Forget();
        }

        // Labels are filled now; the reveal waits for the loss cinematic to open the gate.
        private void OnGameOver(GameOverMessage msg)
        {
            CancelOffer();
            _offerCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            _deathLevel = msg.FinalLevel;
            _finalLevel.Set(msg.FinalLevel);
            _finalScore.Set(msg.FinalScore);
            _bestLevel.Set(_runMeta.BestLevel.Value);
            _bestScore.Set(_runMeta.BestScore.Value);
            ShowRetryOffer(_failureOffers.Current);
            RevealAfterGateAsync().Forget();
        }

        private async UniTaskVoid RetryAsync(bool withBonusCard, CancellationToken ct)
        {
            _claimingRetry = true;
            _canvasGroup.interactable = false;
            bool staged;
            try
            {
                staged = withBonusCard
                    ? await _failureOffers.TryBonusRetryAsync(_deathLevel, ct)
                    : await _failureOffers.TryRetryAsync(_deathLevel, ct);
            }
            finally
            {
                _claimingRetry = false;
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (!staged)
            {
                // A skipped ad leaves the screen up with the same offers.
                _canvasGroup.interactable = true;
                ShowRetryOffer(_failureOffers.Current);
                return;
            }

            Dismiss();
        }

        private void Dismiss()
        {
            CancelOffer();
            SetVisible(false);
            _dismissedPublisher.Publish(new GameOverDismissedMessage());
        }

        private void CancelOffer()
        {
            if (_offerCts == null)
            {
                return;
            }

            _offerCts.Cancel();
            _offerCts.Dispose();
            _offerCts = null;
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

        private void ShowRetryOffer(FailureOffer offer)
        {
            SetRetryInteractable(offer.CanRetry);
            if (_bonusRetryButton != null)
            {
                _bonusRetryButton.gameObject.SetActive(offer.CanBonusRetry);
            }

            if (_retryLabel == null)
            {
                return;
            }

            if (offer.RetryNeedsAd)
            {
                _retryLabel.text = "Retry (ad)";
            }
            else if (offer.RetriesLeft < 0)
            {
                _retryLabel.text = "Retry";
            }
            else
            {
                _retryLabel.text = $"Retry ({offer.RetriesLeft})";
            }
        }

        private void SetRetryInteractable(bool interactable)
        {
            if (_retryButton != null)
            {
                _retryButton.interactable = interactable;
            }
        }
    }
}
