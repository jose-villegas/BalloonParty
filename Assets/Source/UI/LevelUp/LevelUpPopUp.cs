using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI.LevelUp
{
    public class LevelUpPopUp : MonoBehaviour
    {
        private static readonly int AppearTrigger = Animator.StringToHash("Appear");
        private static readonly int HideTrigger = Animator.StringToHash("Hide");
        [Header("References")] [SerializeField]
        private Animator _animator;

        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private Image _levelGlowFill;
        [SerializeField] private ParticleSystem _levelGlowFillParticleSystem;

        [Header("Timing")] [SerializeField] private float _fillAnimationDelay;

        [SerializeField] private float _playParticlesDelay;
        [SerializeField] private float _continueUnpauseDelay;

        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        [Inject] private IPublisher<LevelUpDismissedMessage> _dismissedPublisher;
        [Inject] private IReadyGate _gate;

        private readonly CompositeDisposable _disposable = new();

        private void Start()
        {
            _animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            _levelUpSubscriber
                .Subscribe(msg => ShowAfterGateAsync(msg).Forget())
                .AddTo(_disposable);
        }

        private void OnDestroy()
        {
            _disposable.Dispose();
        }

        public void OnContinue()
        {
            _animator.ResetTrigger(AppearTrigger);
            _animator.SetTrigger(HideTrigger);
            _dismissedPublisher.Publish(new LevelUpDismissedMessage());
            ResumeAfterDelayAsync().Forget();
        }

        private async UniTaskVoid ShowAfterGateAsync(ScoreLevelUpMessage msg)
        {
            await _gate.WaitAsync(destroyCancellationToken);

            Time.timeScale = 0f;

            _levelLabel.text = (msg.NewLevel - 1).ToString("N0");
            // Stale HideTrigger from the previous dismiss would instantly close the popup
            _animator.ResetTrigger(HideTrigger);
            _animator.SetTrigger(AppearTrigger);

            LevelGlowFillAsync(msg.NewLevel).Forget();
        }

        private async UniTask LevelGlowFillAsync(int newLevel)
        {
            await UniTask.Delay(
                (int)(_playParticlesDelay * 1000),
                true,
                cancellationToken: destroyCancellationToken);

            var duration = _levelGlowFillParticleSystem.main.duration;
            var elapsed = 0f;

            _levelGlowFillParticleSystem.Stop();
            _levelGlowFillParticleSystem.Play();

            await UniTask.Delay(
                (int)(_fillAnimationDelay * 1000),
                true,
                cancellationToken: destroyCancellationToken);

            while (elapsed <= duration)
            {
                _levelGlowFill.fillAmount = elapsed / duration;
                elapsed += Time.unscaledDeltaTime;
                await UniTask.Yield(destroyCancellationToken);
            }

            _levelGlowFill.fillAmount = 1f;
            _levelLabel.text = newLevel.ToString("N0");
        }

        private async UniTaskVoid ResumeAfterDelayAsync()
        {
            await UniTask.Delay(
                (int)(_continueUnpauseDelay * 1000),
                true,
                cancellationToken: destroyCancellationToken);
        }
    }
}
