using System.Collections.Generic;
using System.Threading;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.GameOver
{
    /// <summary>
    ///     The loss screen: the level reached and its points, the best run, and the ways on: retry the level
    ///     (the primary action), retry with a bonus card, or start over. Visibility is driven by a
    ///     <see cref="CanvasGroup"/> so the GameObject can stay active and keep its subscriptions alive (never
    ///     toggle with SetActive). The layout is built by <c>Editor/GameOverLayoutBuilder</c>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GameOverScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _finalLevelLabel;
        [SerializeField] private TMP_Text _finalScoreLabel;

        [Tooltip("Its authored text is the template: {0} is the best level, {1} the best score.")]
        [SerializeField] private TMP_Text _bestLabel;

        [SerializeField] private UnityEngine.UI.Button _retryButton;
        [SerializeField] private TMP_Text _retryLabel;

        [Tooltip("{0} is the level the retry starts at.")]
        [SerializeField] private string _retryFormat = "Retry level {0}";

        [Tooltip("The fill of the first heart pinned on Retry's edge, one per free retry (FailureOffer." +
            "FreeRetries). The rest are copies of its pin, made as needed. Hidden with no free retries to count. " +
            "Optional.")]
        [SerializeField] private UnityEngine.UI.Image _retryPinFill;

        [Tooltip("A pin's fill while its retry is still free to use: the HUD hearts' red.")]
        [SerializeField] private Color _pinLeftColor = new(1f, 0.27f, 0f, 1f);

        [Tooltip("A pin's fill once its retry is used.")]
        [SerializeField] private Color _pinUsedColor = new(0.06f, 0.14f, 0.23f, 0.35f);

        [Tooltip("The ad mark inside Retry, shown once the free retries are used. The label keeps clear of it. " +
            "Optional.")]
        [SerializeField] private RectTransform _retryAdBadge;

        [Tooltip("Space between the label and the ad mark.")]
        [SerializeField] [Min(0f)] private float _adBadgeGap = 4f;

        [Tooltip("The heading and row of ways to make the retry easier; hidden while none is on offer. Optional.")]
        [SerializeField] private GameObject _offers;

        [Tooltip("Retries with a bonus card for that attempt, bought with a rewarded ad. Hidden while no bonus " +
            "card can be dealt. Optional.")]
        [SerializeField] private UnityEngine.UI.Button _bonusRetryButton;

        [Header("Motion")]
        [Tooltip("Pops in when the screen shows. Optional.")]
        [SerializeField] private Transform _panel;

        [Tooltip("Pulses now and then while the screen is up, to draw the eye to it. Optional.")]
        [SerializeField] private Transform _primaryButton;

        [SerializeField] [Min(0f)] private float _entranceScale = 0.85f;
        [SerializeField] [Min(0f)] private float _entranceSeconds = 0.35f;
        [SerializeField] [Min(0f)] private float _entranceOvershoot = 1.3f;

        [Tooltip("Taps are ignored this long after the screen shows, so a tap meant for the board can't " +
            "dismiss it.")]
        [SerializeField] [Min(0f)] private float _inputGuardSeconds = 0.3f;

        [SerializeField] [Min(0f)] private float _pulseDelaySeconds = 1.2f;
        [SerializeField] [Min(0.1f)] private float _pulsePeriodSeconds = 2.5f;
        [SerializeField] [Min(1f)] private float _pulseScale = 1.05f;

        [Inject] private ISubscriber<GameOverMessage> _gameOverSubscriber;
        [Inject] private IRunMeta _runMeta;
        [Inject] private IFailureOffers _failureOffers;
        [Inject] private IPublisher<GameOverDismissedMessage> _dismissedPublisher;
        [Inject] private GameOverPresentationGate _presentationGate;

        private readonly List<UnityEngine.UI.Image> _pinFills = new();

        private CanvasGroup _canvasGroup;
        private Vector4 _retryLabelMargin;
        private FormattedLabel _finalLevel;
        private FormattedLabel _finalScore;
        private FormattedLabel _best;
        private int _deathLevel;
        private bool _claimingRetry;
        private Sequence _entrance;
        private Sequence _pulse;

        // One per shown offer: cancelled when the screen dismisses or navigation leaves GameOver, so an ad
        // still playing can't stage a retry for a screen that's gone.
        private CancellationTokenSource _offerCts;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();

            // Wrap before the label text is overwritten so FormattedLabel captures the template.
            _finalLevel = new FormattedLabel(_finalLevelLabel);
            _finalScore = new FormattedLabel(_finalScoreLabel);
            _best = new FormattedLabel(_bestLabel);
            if (_retryLabel != null)
            {
                _retryLabelMargin = _retryLabel.margin;
            }

            if (_retryPinFill != null)
            {
                _pinFills.Add(_retryPinFill);
            }

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
            KillMotion();
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
            _best.Set(_runMeta.BestLevel.Value, _runMeta.BestScore.Value);
            ShowRetryOffer(_failureOffers.Current);
            RevealAfterGateAsync(_offerCts.Token).Forget();
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

        // ct is the offer's: a restart before the reveal (or leaving GameOver) cancels it, so the screen can't
        // pop up over the run that restart started.
        private async UniTaskVoid RevealAfterGateAsync(CancellationToken ct)
        {
            if (await _presentationGate.WaitAsync(ct).SuppressCancellationThrow())
            {
                return;
            }

            SetVisible(true);
            PlayEntrance();
        }

        private void SetVisible(bool visible)
        {
            KillMotion();
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        // Unscaled: the screen shows over the loss cinematic's slowed-down frame.
        private void PlayEntrance()
        {
            _canvasGroup.interactable = false;
            _entrance = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _entrance.Append(_canvasGroup.DOFade(1f, _entranceSeconds * 0.6f).From(0f));
            if (_panel != null)
            {
                _panel.localScale = Vector3.one * _entranceScale;
                _entrance.Join(_panel.DOScale(1f, _entranceSeconds).SetEase(Ease.OutBack, _entranceOvershoot));
            }

            _entrance.InsertCallback(_inputGuardSeconds, () => _canvasGroup.interactable = !_claimingRetry);

            if (_primaryButton == null)
            {
                return;
            }

            var half = Mathf.Min(0.3f, _pulsePeriodSeconds * 0.25f);
            _pulse = DOTween.Sequence().SetUpdate(true).SetLink(gameObject).SetDelay(_pulseDelaySeconds)
                .Append(_primaryButton.DOScale(_pulseScale, half).SetEase(Ease.InOutSine))
                .Append(_primaryButton.DOScale(1f, half).SetEase(Ease.InOutSine))
                .AppendInterval(_pulsePeriodSeconds - 2f * half)
                .SetLoops(-1);
        }

        private void KillMotion()
        {
            _entrance?.Kill();
            _pulse?.Kill();
            _entrance = null;
            _pulse = null;
            if (_panel != null)
            {
                _panel.localScale = Vector3.one;
            }

            if (_primaryButton != null)
            {
                _primaryButton.localScale = Vector3.one;
            }
        }

        private void ShowRetryOffer(FailureOffer offer)
        {
            SetRetryInteractable(offer.CanRetry);
            if (_bonusRetryButton != null)
            {
                _bonusRetryButton.gameObject.SetActive(offer.CanBonusRetry);
            }

            if (_offers != null)
            {
                _offers.SetActive(offer.CanBonusRetry);
            }

            ShowAdBadge(offer.RetryNeedsAd);
            ShowPins(offer.FreeRetries, offer.FreeRetriesLeft);
            if (_retryLabel != null)
            {
                _retryLabel.text = _retryFormat.FormatOrRaw(_deathLevel);
            }
        }

        private void ShowAdBadge(bool shown)
        {
            if (_retryAdBadge == null)
            {
                return;
            }

            _retryAdBadge.gameObject.SetActive(shown);
            if (_retryLabel != null)
            {
                var margin = _retryLabelMargin;
                if (shown)
                {
                    margin.z += _retryAdBadge.rect.width + _adBadgeGap;
                }

                _retryLabel.margin = margin;
            }
        }

        // The first pin is the authored one; more are copied from it the first time a run needs them.
        private void ShowPins(int total, int left)
        {
            if (_retryPinFill == null)
            {
                return;
            }

            var pin = _retryPinFill.transform.parent;
            var row = pin.parent;
            row.gameObject.SetActive(total > 0);
            var fillIndex = _retryPinFill.transform.GetSiblingIndex();
            while (_pinFills.Count < total)
            {
                var copy = Instantiate(pin, row, false);
                _pinFills.Add(copy.GetChild(fillIndex).GetComponent<UnityEngine.UI.Image>());
            }

            for (var i = 0; i < _pinFills.Count; i++)
            {
                _pinFills[i].transform.parent.gameObject.SetActive(i < total);
                _pinFills[i].color = i < left ? _pinLeftColor : _pinUsedColor;
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
