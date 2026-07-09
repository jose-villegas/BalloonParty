using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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

        [Tooltip("Its flexibleWidth tweens 0↔1 so the bar grows in / shrinks out as its color is " +
                 "introduced or gated; the HorizontalLayoutGroup redistributes the rest. Excluded from " +
                 "layout only once fully hidden.")]
        [SerializeField] private LayoutElement _layoutElement;

        [Tooltip("Seconds for a bar to grow in / shrink out when its color is introduced or gated.")]
        [SerializeField] private float _visibilityTweenDuration = 0.4f;

        [Header("Visuals")] [SerializeField] private Graphic[] _graphicsToSetColor;

        [Header("Progress")] [SerializeField] private Slider _progressSlider;

        [Header("Feedback")] [SerializeField] private Animator _animator;

        [SerializeField] private ParticleSystem _completionParticleSystem;
        [SerializeField] private ProgressNotice _pointNoticePrefab;
        [SerializeField] private ProgressNotice _streakNoticePrefab;

        [Inject] private IGamePalette _palette;
        [Inject] private IActiveLevelParameters _levelParams;
        [Inject] private ILevelThresholds _thresholds;
        [Inject] private ISubscriber<StreakChangedMessage> _streakChangedSubscriber;
        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        [Inject] private ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        [Inject] private ISubscriber<LevelUpGlowTrailsMessage> _glowTrailsSubscriber;
        [Inject] private ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        [Inject] private ISubscriber<LevelTransitionCompletedMessage> _transitionCompletedSubscriber;
        [Inject] private ISubscriber<RunResetMessage> _resetSubscriber;
        [Inject] private PoolManager _poolManager;
        [Inject] private ILevelProgress _levelProgress;
        [Inject] private IColorStreak _streakTracker;
        [Inject] private ScoreTrailService _scoreTrailService;

        private ProgressNoticePresenter _notices;
        private PaletteEntry _colorConfig;
        private int _stashedMaxValue;
        private int _shownStreak;
        private bool _active;
        private Tween _flexTween;

        public Vector3 Center => RectAnchorMath.Center((RectTransform)transform);

        // The level-up ceremony is running (popup + Ascent) until the FSM returns to Playing — during it
        // scoring is closed, so trail arrivals / score points shouldn't spawn notices or move the slider.
        private bool LevelUpInProgress => _levelProgress.Phase.Value != LevelUpPhase.Playing;

        private void Awake()
        {
            // Self-heal prefabs predating these fields.
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

            var required = _levelProgress.GetRequiredPoints();
            _progressSlider.maxValue = required;
            _progressSlider.value = _levelProgress.GetProgress(_colorConfig.Name);

            _scoreTrailService.RegisterTarget(_colorConfig.Name, this, _colorConfig.Color);
            ApplyVisibility(animate: false);

            _streakChangedSubscriber.Subscribe(_ => OnStreakChanged()).AddTo(this);
            _levelUpSubscriber.Subscribe(OnLevelUp).AddTo(this);
            _trailArrivedSubscriber.Subscribe(OnTrailArrived).AddTo(this);
            _glowTrailsSubscriber.Subscribe(OnGlowTrails).AddTo(this);
            _dismissedSubscriber.Subscribe(_ => OnDismissed()).AddTo(this);
            _transitionCompletedSubscriber.Subscribe(_ => OnTransitionCompleted()).AddTo(this);
            _resetSubscriber.Subscribe(_ => OnRunReset()).AddTo(this);
        }

        // Driven by the streak signal (any colour), so we also catch this colour's streak being lost when a
        // different colour is popped — not just its own pops. The notice persists until the streak grows
        // into a new value (re-pop) or drops out of multiplier range (dismiss).
        private void OnStreakChanged()
        {
            if (LevelUpInProgress)
            {
                return;
            }

            var streak = _streakTracker.GetStreak(_colorConfig.Name);
            if (streak == _shownStreak)
            {
                return;
            }

            _shownStreak = streak;
            if (streak > 1)
            {
                _notices.ShowStreak(streak);
            }
            else
            {
                _notices.DismissStreak();
            }
        }

        private void OnLevelUp(ScoreLevelUpMessage msg)
        {
            _stashedMaxValue = _thresholds.PointsRequiredForLevel(msg.NewLevel + 1);
            ClearCompletionVfx();
            // Clear existing notices. New ones stay suppressed for the rest of the ceremony via the
            // level FSM phase (see OnTrailArrived) — score trails still in flight keep arriving and would
            // otherwise spawn notices behind the popup.
            _notices.DismissAllNotices();
            _shownStreak = 0;
        }

        // Not called from OnLevelUp — subscriber order is unenforced, so this runs only at
        // Start/OnRunReset (snap) and OnTransitionCompleted (animated, when a level change alters the
        // colour set — deferred to when the board has settled and the player can fire again).
        private void ApplyVisibility(bool animate)
        {
            var active = IsColorActive();
            if (animate && active == _active)
            {
                return;
            }

            _active = active;
            _visibilityGroup.interactable = active;
            _visibilityGroup.blocksRaycasts = active;

            var targetFlex = active ? 1f : 0f;
            var targetAlpha = active ? 1f : 0f;

            _flexTween?.Kill();
            _visibilityGroup.DOKill();

            if (!animate)
            {
                _layoutElement.ignoreLayout = !active;
                _layoutElement.flexibleWidth = targetFlex;
                _visibilityGroup.alpha = targetAlpha;
                return;
            }

            // Tween flexibleWidth so the HorizontalLayoutGroup redistributes across the other bars each
            // frame (they adapt without their own tween). Stay in layout for the whole tween; only drop
            // out once fully hidden so a shrunk-away bar leaves no gap. Unscaled so it plays at a steady
            // rate through the level-up time ramp.
            _layoutElement.ignoreLayout = false;
            _flexTween = DOTween
                .To(() => _layoutElement.flexibleWidth, v => _layoutElement.flexibleWidth = v, targetFlex,
                    _visibilityTweenDuration)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() => _layoutElement.ignoreLayout = !active);
            _visibilityGroup.DOFade(targetAlpha, _visibilityTweenDuration).SetUpdate(true).SetLink(gameObject);
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
            _progressSlider.maxValue = _levelProgress.GetRequiredPoints();
            _progressSlider.value = _levelProgress.GetProgress(_colorConfig.Name);
            ClearCompletionVfx();
            _notices.DismissAllNotices();
            _shownStreak = 0;
            ApplyVisibility(animate: false);
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
        }

        // The reveal/hide tween waits for the transition to finish (board settled, thrower resumed) so
        // the new colour bar slides in exactly when the player regains control, not at popup dismissal.
        private void OnTransitionCompleted()
        {
            ApplyVisibility(animate: true);
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
            // Trails still in flight when the level-up began keep arriving; ignore them for the ceremony
            // so no notices/slider changes land behind the popup.
            if (LevelUpInProgress || msg.ColorName != _colorConfig.Name)
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

        public Vector3 RandomPosition()
        {
            return RectAnchorMath.RandomPosition((RectTransform)transform);
        }
    }
}
