using BalloonParty.Balloon.Spawner;
using BalloonParty.Game.Danger;
using BalloonParty.Shared.Animation;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI.Danger
{
    /// <summary>
    ///     Displays how many hearts the next line will cost. Fades in when danger is non-zero, shows
    ///     "-{N}" via rolling text, and is the countdown to that line: the shine sweep starts each
    ///     interval at the danger level's speed and quickens to full speed as the line arrives.
    /// </summary>
    /// <remarks>
    ///     The shine is the <c>BalloonParty/Sprite/ShinyDefault</c> material on
    ///     <see cref="_shineImage" />, driven through its <c>_ShineSpeed</c> property. Tick that
    ///     material's <b>Shine Uses Unscaled Time</b> if the sweep should keep moving while a popup
    ///     holds <c>Time.timeScale</c> at 0.
    /// </remarks>
    internal sealed class DangerHeartLossView : MonoBehaviour
    {
        private static readonly int ShineSpeedId = Shader.PropertyToID("_ShineSpeed");

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RollingTextAnimator _rollingText;

        [Tooltip("Its material must use BalloonParty/Sprite/ShinyDefault. The material is cloned at " +
            "Awake, so the shared asset is never written to.")]
        [SerializeField] private Image _shineImage;

        [Header("Alpha")]
        [SerializeField] private float _fadeSpeed = 6f;

        [Header("Shine Speed (sweeps per second)")]
        [SerializeField] private float _shineSpeedAtNoDanger = 0.5f;
        [SerializeField] private float _shineSpeedAtMaxDanger = 3.5f;

        private IDangerLevel _danger;
        private ISpawnClock _spawnClock;

        private Material _shineMaterial;
        private float _dangerLevel;
        private float _shineLevel;
        private float _targetAlpha;
        private CompositeDisposable _subscriptions;

        private void Awake()
        {
            if (_shineImage == null)
            {
                return;
            }

            // Cloned, not shared: assigning Image.material keeps the asset itself, so writing
            // _ShineSpeed on it would edit the project asset in the editor and bleed across every
            // other user of that material at runtime.
            _shineMaterial = new Material(_shineImage.material);
            _shineImage.material = _shineMaterial;

            // Construct runs from the scope's build callback, which may land before Awake, and
            // subscribing to a ReactiveProperty fires immediately — so the first level can arrive
            // with no material to write to. Re-apply whatever it was rather than depend on the order.
            ApplyShineSpeed();
        }

        private void Update()
        {
            if (_targetAlpha > 0f && _spawnClock != null)
            {
                _shineLevel = Mathf.Lerp(_dangerLevel, 1f, _spawnClock.NextLineProgress);
                ApplyShineSpeed();
            }

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

            if (_shineMaterial != null)
            {
                Destroy(_shineMaterial);
            }
        }

        [Inject]
        private void Construct(IDangerLevel danger, ISpawnClock spawnClock)
        {
            _danger = danger;
            _spawnClock = spawnClock;

            _subscriptions = new CompositeDisposable();

            _danger.HeartsAtRisk
                .Subscribe(OnHeartsAtRiskChanged)
                .AddTo(_subscriptions);

            _danger.Level
                .Subscribe(OnDangerLevelChanged)
                .AddTo(_subscriptions);

            _canvasGroup.alpha = 0f;
        }

        private void OnHeartsAtRiskChanged(int hearts)
        {
            _targetAlpha = hearts > 0 ? 1f : 0f;

            if (hearts > 0)
            {
                _rollingText.SetThousands(-hearts);
            }
        }

        // Speed, not duration: the shader sweeps at frac(time * _ShineSpeed), so this is cycles per
        // second and rises with danger — the inverse of the duration the old UIShiny player took.
        private void OnDangerLevelChanged(float level)
        {
            _dangerLevel = level;
            _shineLevel = level;
            ApplyShineSpeed();
        }

        private void ApplyShineSpeed()
        {
            if (_shineMaterial != null)
            {
                _shineMaterial.SetFloat(ShineSpeedId,
                    Mathf.Lerp(_shineSpeedAtNoDanger, _shineSpeedAtMaxDanger, _shineLevel));
            }
        }
    }
}
