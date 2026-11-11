using BalloonParty.Game.CardReveal;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.UI.HeadsUp;
using DG.Tweening;
using MessagePipe;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.Tooltip
{
    /// <summary>
    ///     The on-screen tooltip: shows each <see cref="TooltipMessage" /> briefly, then fades it out. A new
    ///     line replaces the one showing. Also words a retry's <see cref="BonusCardDealtMessage" />, held until
    ///     play resumes: the card is dealt during the reset, behind the game-over screen.
    /// </summary>
    public class TooltipBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _label;

        [Tooltip("Optional: writes each line on in pencil, on _label. Unassigned, a line appears whole.")]
        [SerializeField] private WriteOnText _writeOn;
        [SerializeField] [Min(0f)] private float _fadeDuration = 0.3f;
        [SerializeField] [Min(0f)] private float _holdDuration = 1.6f;
        [SerializeField] [Range(0f, 1f)] private float _visibleAlpha = 0.7f;

        [Tooltip("Names a retry's bonus card as play resumes; {0} is the card's title.")]
        [SerializeField] private string _bonusCardFormat = "Bonus card: {0}";

        [Inject] private ISubscriber<TooltipMessage> _tooltipSubscriber;
        [Inject] private ISubscriber<BonusCardDealtMessage> _bonusDealtSubscriber;
        [Inject] private ICardReveals _cardReveals;

        private Sequence _sequence;
        private string _pendingBonus;

        private void Awake()
        {
            _group.alpha = 0f;
        }

        private void Start()
        {
            _tooltipSubscriber.Subscribe(message => Show(message.Text)).AddTo(this);
            _bonusDealtSubscriber.Subscribe(OnBonusDealt).AddTo(this);
            Navigation.Current
                .Where(state => state == NavigationState.Game)
                .Subscribe(_ => ShowPendingBonus())
                .AddTo(this);
        }

        private void OnDestroy()
        {
            _sequence?.Kill();
        }

        private void OnBonusDealt(BonusCardDealtMessage message)
        {
            // With a reveal on stage the bonus card is its to show.
            if (_cardReveals.HasStage)
            {
                return;
            }

            _pendingBonus = _bonusCardFormat.FormatOrRaw(message.Card.Title);
            if (Navigation.Current.Value == NavigationState.Game)
            {
                ShowPendingBonus();
            }
        }

        private void ShowPendingBonus()
        {
            if (_pendingBonus == null)
            {
                return;
            }

            var text = _pendingBonus;
            _pendingBonus = null;
            Show(text);
        }

        private void Show(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _sequence?.Kill();

            // Written once the fade-in is done (a short line would otherwise be written while still
            // nearly invisible), and the hold lengthened by the write so the line still gets its full hold.
            var hold = _holdDuration;
            if (_writeOn != null)
            {
                _writeOn.Write(text, _fadeDuration);
                hold += _writeOn.Duration - _fadeDuration;
            }
            else
            {
                _label.text = text;
            }

            // Unscaled: the lines land in slow-motion beats and frozen screens.
            _sequence = DOTween.Sequence()
                .Append(_group.DOFade(_visibleAlpha, _fadeDuration))
                .AppendInterval(hold)
                .Append(_group.DOFade(0f, _fadeDuration))
                .SetUpdate(true);
        }
    }
}
