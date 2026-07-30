using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.Tooltip
{
    /// <summary>
    ///     Shows a one-time "hold to speed up" hint after the projectile has been in flight for
    ///     <see cref="IProjectileFlightConfig.HoldSpeedUpTooltipDelay"/> seconds. Once dismissed
    ///     it never appears again (persisted via PlayerPrefs).
    /// </summary>
    internal sealed class HoldSpeedUpTooltip : MonoBehaviour
    {
        private const string SeenKey = "HoldSpeedUpTooltipSeen";

        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Timing")]
        [SerializeField] private float _fadeDuration = 0.4f;
        [SerializeField] private float _holdDuration = 2.5f;
        [SerializeField] [Range(0f, 1f)] private float _targetAlpha = 1f;

        private IProjectileFlightConfig _config;
        private CompositeDisposable _subscriptions;

        private bool _alreadySeen;
        private bool _inFlight;
        private float _flightElapsed;
        private Phase _phase;
        private float _phaseElapsed;

        private void Update()
        {
            if (_config == null || _alreadySeen)
            {
                return;
            }

            if (_inFlight && _phase == Phase.Idle)
            {
                _flightElapsed += Time.unscaledDeltaTime;

                if (_config.HoldSpeedUpTooltipDelay > 0f &&
                    _flightElapsed >= _config.HoldSpeedUpTooltipDelay)
                {
                    BeginShow();
                }
            }

            UpdatePhase();
        }

        private void OnDestroy()
        {
            _subscriptions?.Dispose();
        }

        [Inject]
        private void Construct(
            IProjectileFlightConfig config,
            ISubscriber<ProjectileFiredMessage> firedSubscriber,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber)
        {
            _config = config;
            _alreadySeen = PlayerPrefs.GetInt(SeenKey, 0) == 1;
            _canvasGroup.alpha = 0f;

            _subscriptions = new CompositeDisposable();

            firedSubscriber
                .Subscribe(_ => OnFired())
                .AddTo(_subscriptions);
            destroyedSubscriber
                .Subscribe(_ => OnProjectileDestroyed())
                .AddTo(_subscriptions);
        }

        private void OnFired()
        {
            _inFlight = true;
            _flightElapsed = 0f;
        }

        private void OnProjectileDestroyed()
        {
            _inFlight = false;
            _flightElapsed = 0f;

            // If showing when projectile dies, skip straight to fade-out.
            if (_phase == Phase.FadeIn || _phase == Phase.Hold)
            {
                _phase = Phase.FadeOut;
                _phaseElapsed = 0f;
            }
        }

        private void BeginShow()
        {
            _phase = Phase.FadeIn;
            _phaseElapsed = 0f;
        }

        private void UpdatePhase()
        {
            switch (_phase)
            {
                case Phase.Idle:
                    return;

                case Phase.FadeIn:
                    _phaseElapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = Mathf.Lerp(0f, _targetAlpha, Mathf.Clamp01(_phaseElapsed / _fadeDuration));

                    if (_phaseElapsed >= _fadeDuration)
                    {
                        _phase = Phase.Hold;
                        _phaseElapsed = 0f;
                    }

                    break;

                case Phase.Hold:
                    _phaseElapsed += Time.unscaledDeltaTime;

                    if (_phaseElapsed >= _holdDuration)
                    {
                        _phase = Phase.FadeOut;
                        _phaseElapsed = 0f;
                    }

                    break;

                case Phase.FadeOut:
                    _phaseElapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = Mathf.Lerp(_targetAlpha, 0f, Mathf.Clamp01(_phaseElapsed / _fadeDuration));

                    if (_phaseElapsed >= _fadeDuration)
                    {
                        _phase = Phase.Idle;
                        _canvasGroup.alpha = 0f;
                        MarkAsSeen();
                    }

                    break;
            }
        }

        private void MarkAsSeen()
        {
            _alreadySeen = true;
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
        }

        private enum Phase
        {
            Idle,
            FadeIn,
            Hold,
            FadeOut
        }
    }
}
