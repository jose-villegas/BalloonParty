using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BalloonParty.Configuration.Palette;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.UI.Score
{
    public class ColorProgressBar : MonoBehaviour, ITrailEndpoint
    {
        private static readonly int CompletedParam = Animator.StringToHash("Completed");
        private static readonly int TrailHitTrigger = Animator.StringToHash("TrailHit");
#if UNITY_EDITOR
        private static readonly ConfigAssetCache<GamePalette> PaletteCache = new();
#endif

        [Header("Configuration")] [PaletteColorName] [SerializeField]
        private string _colorName;

        [Tooltip("Toggled (not SetActive) when this color is gated out of the active level range.")]
        [SerializeField] private CanvasGroup _visibilityGroup;

        [Tooltip("Excluded from a parent Layout Group's sizing when gated out, so a hidden bar " +
                 "doesn't leave a gap in the row.")]
        [SerializeField] private LayoutElement _layoutElement;

        [Header("Visuals")] [SerializeField] private Graphic[] _graphicsToSetColor;

        [Header("Progress")] [SerializeField] private Slider _progressSlider;

        [Header("Feedback")] [SerializeField] private Animator _animator;

        [SerializeField] private ParticleSystem _completionParticleSystem;
        [SerializeField] private ProgressNotice _pointNoticePrefab;
        [SerializeField] private ProgressNotice _streakNoticePrefab;

        [Inject] private IGamePalette _palette;
        [Inject] private IActiveLevelParameters _levelParams;
        [Inject] private ISubscriber<ScorePointMessage> _scoredSubscriber;
        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        [Inject] private ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        [Inject] private ISubscriber<LevelUpGlowTrailsMessage> _glowTrailsSubscriber;
        [Inject] private ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        [Inject] private ISubscriber<RunResetMessage> _resetSubscriber;
        [Inject] private PoolManager _poolManager;
        [Inject] private IScoreQuery _scoreController;
        [Inject] private IColorStreak _streakTracker;
        [Inject] private ScoreTrailService _scoreTrailService;

        private ProgressNoticePresenter _notices;
        private PaletteEntry _colorConfig;
        private int _stashedMaxValue;

        private void Awake()
        {
            // Self-heal a prefab that predates these fields, rather than NRE at Start.
            if (_visibilityGroup == null)
            {
                _visibilityGroup = GetComponent<CanvasGroup>();
            }

            if (_visibilityGroup == null)
            {
                _visibilityGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (_layoutElement == null)
            {
                _layoutElement = GetComponent<LayoutElement>();
            }

            if (_layoutElement == null)
            {
                _layoutElement = gameObject.AddComponent<LayoutElement>();
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(_colorName) || _graphicsToSetColor == null)
            {
                return;
            }

            var palette = PaletteCache.Value;
            if (palette == null)
            {
                return;
            }

            var entry = palette.GetEntry(_colorName);
            if (entry == null)
            {
                return;
            }

            foreach (var g in _graphicsToSetColor)
            {
                if (g != null)
                {
                    g.color = entry.Color.WithAlpha(g.color.a);
                }
            }
#endif
        }

        private void Start()
        {
            _colorConfig = _palette.GetEntry(_colorName);
            _notices = new ProgressNoticePresenter(
                _poolManager,
                _pointNoticePrefab,
                _streakNoticePrefab,
                transform,
                _colorConfig.Name,
                _colorConfig.Color);

            foreach (var g in _graphicsToSetColor)
            {
                g.color = _colorConfig.Color.WithAlpha(g.color.a);
            }

            var required = _scoreController.GetRequiredPoints();
            _progressSlider.maxValue = required;
            _progressSlider.value = _scoreController.GetProgress(_colorConfig.Name);

            _scoreTrailService.RegisterTarget(_colorConfig.Name, this, _colorConfig.Color);
            ApplyVisibility();

            _scoredSubscriber.Subscribe(OnScorePoint).AddTo(this);
            _levelUpSubscriber.Subscribe(OnLevelUp).AddTo(this);
            _trailArrivedSubscriber.Subscribe(OnTrailArrived).AddTo(this);
            _glowTrailsSubscriber.Subscribe(OnGlowTrails).AddTo(this);
            _dismissedSubscriber.Subscribe(_ => OnDismissed()).AddTo(this);
            _resetSubscriber.Subscribe(_ => OnRunReset()).AddTo(this);
        }

        private void OnScorePoint(ScorePointMessage msg)
        {
            if (msg.GroupIndex > 0 || msg.ColorName != _colorConfig.Name)
            {
                return;
            }

            var streak = _streakTracker.GetStreak(_colorConfig.Name);

            if (streak > 1)
            {
                _notices.DismissFullyShownNotices();
                _notices.SpawnStreakNotice(streak);
            }
        }

        private void OnLevelUp(ScoreLevelUpMessage msg)
        {
            _stashedMaxValue = _levelParams.PointsRequiredForLevel(msg.NewLevel + 1);
            ClearCompletionVfx();
        }

        // Not called from OnLevelUp — the resolver may have already re-resolved to the new
        // level's color set by then (subscriber order is unenforced), so this only runs at Start
        // and OnDismissed, after the ceremony ends.
        private void ApplyVisibility()
        {
            var active = IsColorActive();
            _visibilityGroup.alpha = active ? 1f : 0f;
            _visibilityGroup.interactable = active;
            _visibilityGroup.blocksRaycasts = active;
            _layoutElement.ignoreLayout = !active; // CanvasGroup alone doesn't exclude from layout sizing.
        }

        private bool IsColorActive()
        {
            var allowed = _levelParams.Current.AllowedColors;
            for (var i = 0; i < allowed.Count; i++)
            {
                if (allowed[i] == _colorConfig.Name)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnRunReset()
        {
            _progressSlider.maxValue = _scoreController.GetRequiredPoints();
            _progressSlider.value = _scoreController.GetProgress(_colorConfig.Name);
            ClearCompletionVfx();
            _notices.DismissAllNotices();
        }

        private void ClearCompletionVfx()
        {
            _completionParticleSystem.Stop();
            _completionParticleSystem.gameObject.SetActive(false);
            _animator.SetBool(CompletedParam, false);
        }

        private void OnGlowTrails(LevelUpGlowTrailsMessage msg)
        {
            DrainSliderAsync(msg.TrailsPerBar, msg.StaggerDelay).Forget();
        }

        private void OnDismissed()
        {
            _progressSlider.maxValue = _stashedMaxValue;
            _progressSlider.value = 0;
            ClearCompletionVfx();
            ApplyVisibility();
        }

        private async UniTaskVoid DrainSliderAsync(int steps, float staggerDelay)
        {
            var staggerMs = Mathf.RoundToInt(staggerDelay * 1000f);
            var drainPerStep = _progressSlider.value / steps;

            for (var i = 0; i < steps; i++)
            {
                _progressSlider.value = Mathf.Max(0f, _progressSlider.value - drainPerStep);

                if (i < steps - 1)
                {
                    await UniTask.Delay(staggerMs, true,
                        cancellationToken: destroyCancellationToken);
                }
            }

            _progressSlider.value = 0f;
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (msg.ColorName != _colorConfig.Name)
            {
                return;
            }

            _animator.SetTrigger(TrailHitTrigger);
            _progressSlider.value = Mathf.Min(_progressSlider.value + 1, _progressSlider.maxValue);

            var anchored = RectAnchorMath.WorldToAnchoredPosition((RectTransform)transform, msg.WorldPosition);
            _notices.SpawnPointNotice(anchored);

            if (_progressSlider.value >= _progressSlider.maxValue)
            {
                _completionParticleSystem.gameObject.SetActive(true);
                _completionParticleSystem.Play();
                _animator.SetBool(CompletedParam, true);
            }
        }

        public Vector3 Center => RectAnchorMath.Center((RectTransform)transform);

        public Vector3 RandomPosition()
        {
            return RectAnchorMath.RandomPosition((RectTransform)transform);
        }
    }
}
