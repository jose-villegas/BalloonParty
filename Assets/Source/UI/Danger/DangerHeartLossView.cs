using BalloonParty.Game.Danger;
using BalloonParty.Shared.Animation;
using Coffee.UIEffects;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.Danger
{
    /// <summary>
    ///     Displays how many hearts the next wave will cost. Fades in when danger is non-zero,
    ///     speeds up the shine effect proportionally, and shows "-{N}" via rolling text.
    /// </summary>
    internal sealed class DangerHeartLossView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RollingTextAnimator _rollingText;
        [SerializeField] private UIShiny _shiny;

        [Header("Alpha")]
        [SerializeField] private float _fadeSpeed = 6f;

        [Header("Shine Duration")]
        [SerializeField] private float _shinyDurationAtNoDanger = 2f;
        [SerializeField] private float _shinyDurationAtMaxDanger = 0.3f;

        private IDangerLevel _danger;

        private float _targetAlpha;
        private CompositeDisposable _subscriptions;

        [Inject]
        private void Construct(IDangerLevel danger)
        {
            _danger = danger;

            _subscriptions = new CompositeDisposable();

            _danger.HeartsAtRisk
                .Subscribe(OnHeartsAtRiskChanged)
                .AddTo(_subscriptions);

            _danger.Level
                .Subscribe(OnDangerLevelChanged)
                .AddTo(_subscriptions);

            _canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (Mathf.Approximately(_canvasGroup.alpha, _targetAlpha))
            {
                return;
            }

            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }

        private void OnHeartsAtRiskChanged(int hearts)
        {
            _targetAlpha = hearts > 0 ? 1f : 0f;

            if (hearts > 0)
            {
                _rollingText.SetThousands(-hearts);
            }
        }

        private void OnDangerLevelChanged(float level)
        {
            if (_shiny == null)
            {
                return;
            }

            _shiny.effectPlayer.duration = Mathf.Lerp(
                _shinyDurationAtNoDanger, _shinyDurationAtMaxDanger, level);
        }
    }
}
