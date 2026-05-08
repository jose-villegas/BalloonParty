using System.Collections;
using MessagePipe;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;

namespace BalloonParty.UI
{
    public class LevelUpPopUp : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private Text _levelLabel;
        [SerializeField] private Image _levelGlowFill;
        [SerializeField] private ParticleSystem _levelGlowFillParticleSystem;
        [SerializeField] private float _fillAnimationDelay;
        [SerializeField] private float _playParticlesDelay;
        [SerializeField] private float _continueUnpauseDelay;

        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        [Inject] private SlotGrid _grid;

        private readonly CompositeDisposable _disposable = new();

        private void Start()
        {
            _levelUpSubscriber
                .Subscribe(msg => StartCoroutine(WaitForStability(msg.NewLevel)))
                .AddTo(_disposable);
        }

        private void OnDestroy() => _disposable.Dispose();

        public void OnContinue()
        {
            _animator.SetTrigger("Hide");
            StartCoroutine(UnpauseAfterDelay());
        }

        private IEnumerator WaitForStability(int newLevel)
        {
            while (!_grid.AllBalloonsStable())
                yield return null;

            _levelLabel.text = (newLevel - 1).ToString("N0");
            _animator.SetTrigger("Appear");

            yield return LevelGlowFill(newLevel);
        }

        private IEnumerator LevelGlowFill(int newLevel)
        {
            yield return new WaitForSecondsRealtime(_playParticlesDelay);

            var duration = _levelGlowFillParticleSystem.main.duration;
            var elapsed = 0f;

            _levelGlowFillParticleSystem.Stop();
            _levelGlowFillParticleSystem.Play();

            yield return new WaitForSecondsRealtime(_fillAnimationDelay);

            while (elapsed <= duration)
            {
                _levelGlowFill.fillAmount = elapsed / duration;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _levelGlowFill.fillAmount = 1f;
            _levelLabel.text = newLevel.ToString("N0");
        }

        private IEnumerator UnpauseAfterDelay()
        {
            yield return new WaitForSecondsRealtime(_continueUnpauseDelay);
            Time.timeScale = 1f;
        }
    }
}



