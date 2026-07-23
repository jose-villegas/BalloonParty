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
    // Both feedback effects (the per-arrival pulse and the settled completed glow) are DOTween now, so the
    // ColorProgress.controller and both its clips (ScoreTrailHit.anim, ColorProgressCompleted.anim) are dead
    // assets — deleted in-editor together with the Animator component on the bar prefab.
    public class ColorProgressBar : MonoBehaviour, ITrailEndpoint
    {
        private const float PulseScalePeak = 3f;
        private const float PulsePpuPeak = 5f;
        private const float PulsePpuRest = 0.01f;
        private const float CompletedAlphaPeak = 0.502f;
        private const float CompletedPpuPeak = 50f;
        private const float CompletedDuration = 1f;

        private static readonly Vector2 CompletedSizeGrowth = new Vector2(7.5f, 7f);
#if UNITY_EDITOR
        private static GamePalette _cachedPalette;
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

        [Header("Feedback")]
        [Tooltip("Glow-flashed once per frame on arrival, a DOTween port of the retired ScoreTrailHit clip. " +
                 "The outline is invisible at rest (alpha 0, PPU 0.01) — the pulse flashes it in as it expands.")]
        [SerializeField] private RectTransform _pulseOutline;
        [SerializeField] private Image _pulseOutlineImage;
        [SerializeField] private RectTransform _pulseBackground;
        [SerializeField] private RectTransform _pulseFillArea;
        [SerializeField] private float _pulseRise = 0.1667f;
        [SerializeField] private float _pulseTail = 0.8333f;
        [SerializeField] private float _pulseBulge = 2f;
        [SerializeField] private float _pulseAlphaPeak = 0.349f;

        [SerializeField] private ParticleSystem _completionParticleSystem;
        [SerializeField] private ProgressNotice _pointNoticePrefab;
        [SerializeField] private ProgressNotice _streakNoticePrefab;

        [Inject] private IScoreTrailConfig _config;
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
        private bool _pulseQueued;
        private bool _pulseRestCaptured;
        private bool _completed;
        private float _bgRestSizeY;
        private float _fillRestSizeY;
        private Vector2 _outlineRestSizeDelta;
        private float _builtPulseRise;
        private float _builtPulseTail;
        private float _builtPulseBulge;
        private float _builtPulseAlphaPeak;
        private Tween _flexTween;
        private Sequence _pulseSequence;
        private Sequence _completedSequence;

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

            var palette = FindPalette();
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

#if UNITY_EDITOR
        private static GamePalette FindPalette()
        {
            if (_cachedPalette == null)
            {
                var guids = UnityEditor.AssetDatabase.FindAssets("t:GamePalette");
                if (guids.Length > 0)
                {
                    _cachedPalette = UnityEditor.AssetDatabase.LoadAssetAtPath<GamePalette>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            return _cachedPalette;
        }
#endif

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

            // Amortized over frames (destroyCancellationToken ties it to this bar's lifetime) so a bar
            // built at level setup never spikes into a hitch.
            _notices.PrewarmAsync(
                _config.ProgressNoticePrewarmPerColor,
                _config.ProgressNoticePrewarmPerColor,
                destroyCancellationToken).Forget();

            _streakChangedSubscriber.Subscribe(_ => OnStreakChanged()).AddTo(this);
            _levelUpSubscriber.Subscribe(OnLevelUp).AddTo(this);
            _trailArrivedSubscriber.Subscribe(OnTrailArrived).AddTo(this);
            _glowTrailsSubscriber.Subscribe(OnGlowTrails).AddTo(this);
            _dismissedSubscriber.Subscribe(_ => OnDismissed()).AddTo(this);
            _transitionCompletedSubscriber.Subscribe(_ => OnTransitionCompleted()).AddTo(this);
            _resetSubscriber.Subscribe(_ => OnRunReset()).AddTo(this);
        }

        // Coalesce a burst of arrivals into one pulse per frame — the storm this replaces re-triggered an
        // Animator clip every arrival. The reused sequence Restart()s (rewinding to rest) so overlapping
        // pulses never drift.
        private void LateUpdate()
        {
            if (!_pulseQueued)
            {
                return;
            }

            _pulseQueued = false;

            // Pulses are swallowed while the bar sits completed — the retired FSM had no TrailHit exit from
            // its Completed state, and the settled glow ring owns the outline until the level-up drains it.
            if (_completed)
            {
                return;
            }

            Pulse();
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
            _stashedMaxValue = _thresholds.PointsRequiredForLevel(msg.NewLevel);

            // The watermark only fires this once every colour has confirmed its requirement, but the slider
            // sums points as trails LAND — the ones still in flight are frozen behind the popup, so the bar
            // can read low. Snap it full (maxValue is still this level's requirement) so the popup shows the
            // score that was actually reached, and the glow drain starts from a completed bar.
            _progressSlider.value = _progressSlider.maxValue;

            ClearCompletionVfx();
            // Animate existing notices out (not a hard snap) as the popup takes over. New ones stay
            // suppressed for the rest of the ceremony via the level FSM phase (see OnTrailArrived) — score
            // trails still in flight keep arriving and would otherwise spawn notices behind the popup.
            _notices.DismissAllAnimated();
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
            KillAndResetPulse();
            ApplyVisibility(animate: false);
        }

        private void ClearCompletionVfx()
        {
            _completionParticleSystem.Stop();
            _completionParticleSystem.gameObject.SetActive(false);
            SetCompleted(false);
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

            _pulseQueued = true;
            _progressSlider.value = Mathf.Min(_progressSlider.value + msg.Points, _progressSlider.maxValue);

            var anchored = RectAnchorMath.WorldToAnchoredPosition((RectTransform)transform, msg.WorldPosition);
            _notices.SpawnPointNotice(anchored, msg.Points);

            if (_progressSlider.value >= _progressSlider.maxValue)
            {
                _completionParticleSystem.gameObject.SetActive(true);
                _completionParticleSystem.Play();
                SetCompleted(true);
            }
        }

        public Vector3 RandomPosition()
        {
            return RectAnchorMath.RandomPosition((RectTransform)transform);
        }

        // A clamped non-looping Animator state re-applies its bound properties every frame, so a full bar
        // rebuilt its canvas every frame for the whole pre-level-up stretch. This DOTween port of the retired
        // Assets/Animation/Progress/ColorProgressCompleted.anim lights the outline glow ring over 1s and then
        // goes quiet — no writes once it settles. Values mirror the clip (alpha 0→0.502, PPU →50,
        // sizeDelta →rest+(7.5,7)); flat tangents are approximated with Ease.InOutSine. A no-op change is
        // ignored so it stays idempotent like the old SetBool.
        private void SetCompleted(bool completed)
        {
            if (completed == _completed)
            {
                return;
            }

            _completed = completed;

            if (!completed)
            {
                // The old controller exited Completed to a motionless Empty state, so snap the outline back
                // to rest rather than tweening.
                _completedSequence?.Kill();
                _completedSequence = null;

                if (_pulseOutline != null)
                {
                    _pulseOutline.localScale = Vector3.one;
                    _pulseOutline.sizeDelta = _outlineRestSizeDelta;
                }

                if (_pulseOutlineImage != null)
                {
                    _pulseOutlineImage.color = _pulseOutlineImage.color.WithAlpha(0f);
                    _pulseOutlineImage.pixelsPerUnitMultiplier = PulsePpuRest;
                }

                return;
            }

            CapturePulseRest();
            KillAndResetPulse();

            _completedSequence?.Kill();
            _completedSequence = DOTween.Sequence();

            if (_pulseOutlineImage != null)
            {
                _completedSequence.Join(
                    _pulseOutlineImage.DOFade(CompletedAlphaPeak, CompletedDuration).SetEase(Ease.InOutSine));
                _completedSequence.Join(DOTween.To(
                    () => _pulseOutlineImage.pixelsPerUnitMultiplier,
                    v => _pulseOutlineImage.pixelsPerUnitMultiplier = v, CompletedPpuPeak, CompletedDuration)
                    .SetEase(Ease.InOutSine));
            }

            if (_pulseOutline != null)
            {
                _completedSequence.Join(DOTween.To(
                    () => _pulseOutline.sizeDelta, v => _pulseOutline.sizeDelta = v,
                    _outlineRestSizeDelta + CompletedSizeGrowth, CompletedDuration).SetEase(Ease.InOutSine));
            }

            // Unscaled to match the retired clip's UnscaledTime; SetLink kills it if this bar is destroyed.
            // The lit ring persists at rest once this completes — no per-frame writes.
            _completedSequence.SetUpdate(true).SetLink(gameObject);
        }

        // A hand-built DOTween port of the retired Assets/Animation/Progress/ScoreTrailHit.anim — the
        // constants above (rise 0.1667s, scale peak 3, alpha peak 0.349, PPU 5↔0.01, bulge 2, tail 0.8333s)
        // mirror that clip's keyframes so it can be deleted. Every piece is null-guarded independently, so an
        // unwired reference just skips its part of the pulse. The clip's flat tangents (Unity smooth in-out)
        // are approximated with Ease.InOutSine. Ends exactly on rest so nothing drifts across many pulses.
        private void Pulse()
        {
            CapturePulseRest();

            // Reuse one sequence across a burst instead of rebuilding (~10 closures) per arrival: build once
            // from rest (so its captured start values stay at rest), then Restart() on every retrigger. Only
            // rebuild when a serialized knob changed, so live inspector tuning still takes effect.
            if (_pulseSequence == null || PulseKnobsChanged())
            {
                BuildPulseSequence();
                return;
            }

            _pulseSequence.Restart();
        }

        private void BuildPulseSequence()
        {
            _pulseSequence?.Kill();
            ResetPulseProperties();

            _pulseSequence = DOTween.Sequence();

            if (_pulseOutline != null)
            {
                _pulseSequence.Insert(0f, _pulseOutline.DOScale(PulseScalePeak, _pulseRise).SetEase(Ease.InOutSine));
                _pulseSequence.Insert(_pulseRise, _pulseOutline.DOScale(1f, _pulseRise).SetEase(Ease.InOutSine));
            }

            if (_pulseOutlineImage != null)
            {
                _pulseSequence.Insert(0f,
                    _pulseOutlineImage.DOFade(_pulseAlphaPeak, _pulseRise).SetEase(Ease.InOutSine));
                _pulseSequence.Insert(_pulseRise,
                    _pulseOutlineImage.DOFade(0f, _pulseTail).SetEase(Ease.InOutSine));
                _pulseSequence.Insert(0f, DOTween.To(
                    () => _pulseOutlineImage.pixelsPerUnitMultiplier,
                    v => _pulseOutlineImage.pixelsPerUnitMultiplier = v, PulsePpuPeak, _pulseRise)
                    .SetEase(Ease.InOutSine));
                _pulseSequence.Insert(_pulseRise, DOTween.To(
                    () => _pulseOutlineImage.pixelsPerUnitMultiplier,
                    v => _pulseOutlineImage.pixelsPerUnitMultiplier = v, PulsePpuRest, _pulseRise)
                    .SetEase(Ease.InOutSine));
            }

            if (_pulseBackground != null)
            {
                _pulseSequence.Insert(0f, DOTween.To(
                    () => _pulseBackground.sizeDelta.y,
                    v => _pulseBackground.sizeDelta = _pulseBackground.sizeDelta.WithY(v),
                    _bgRestSizeY + _pulseBulge, _pulseRise).SetEase(Ease.InOutSine));
                _pulseSequence.Insert(_pulseRise, DOTween.To(
                    () => _pulseBackground.sizeDelta.y,
                    v => _pulseBackground.sizeDelta = _pulseBackground.sizeDelta.WithY(v),
                    _bgRestSizeY, _pulseTail).SetEase(Ease.InOutSine));
            }

            if (_pulseFillArea != null)
            {
                _pulseSequence.Insert(0f, DOTween.To(
                    () => _pulseFillArea.sizeDelta.y,
                    v => _pulseFillArea.sizeDelta = _pulseFillArea.sizeDelta.WithY(v),
                    _fillRestSizeY + _pulseBulge, _pulseRise).SetEase(Ease.InOutSine));
                _pulseSequence.Insert(_pulseRise, DOTween.To(
                    () => _pulseFillArea.sizeDelta.y,
                    v => _pulseFillArea.sizeDelta = _pulseFillArea.sizeDelta.WithY(v),
                    _fillRestSizeY, _pulseRise).SetEase(Ease.InOutSine));
            }

            // Unscaled to match the retired clip's UnscaledTime; AutoKill off so the sequence is reused across
            // pulses; SetLink kills it if this bar is destroyed.
            _pulseSequence.SetUpdate(true).SetAutoKill(false).SetLink(gameObject);

            _builtPulseRise = _pulseRise;
            _builtPulseTail = _pulseTail;
            _builtPulseBulge = _pulseBulge;
            _builtPulseAlphaPeak = _pulseAlphaPeak;
        }

        private bool PulseKnobsChanged()
        {
            return _builtPulseRise != _pulseRise
                || _builtPulseTail != _pulseTail
                || _builtPulseBulge != _pulseBulge
                || _builtPulseAlphaPeak != _pulseAlphaPeak;
        }

        // The +2 bulge is relative to each rect's authored rest height, captured once before any pulse has
        // touched it so retriggers reset to the true rest rather than an in-flight value. The outline rest
        // sizeDelta is the base the completed glow grows from.
        private void CapturePulseRest()
        {
            if (_pulseRestCaptured)
            {
                return;
            }

            _pulseRestCaptured = true;
            if (_pulseOutline != null)
            {
                _outlineRestSizeDelta = _pulseOutline.sizeDelta;
            }

            if (_pulseBackground != null)
            {
                _bgRestSizeY = _pulseBackground.sizeDelta.y;
            }

            if (_pulseFillArea != null)
            {
                _fillRestSizeY = _pulseFillArea.sizeDelta.y;
            }
        }

        // The reused sequence is rewound (which also pauses it) and every pulse property hard-reset to rest,
        // so a run reset or a completion taking over the outline never leaves an in-flight value behind.
        private void KillAndResetPulse()
        {
            _pulseSequence?.Rewind();
            ResetPulseProperties();
        }

        private void ResetPulseProperties()
        {
            if (!_pulseRestCaptured)
            {
                return;
            }

            if (_pulseOutline != null)
            {
                _pulseOutline.localScale = Vector3.one;
            }

            if (_pulseOutlineImage != null)
            {
                _pulseOutlineImage.color = _pulseOutlineImage.color.WithAlpha(0f);
                _pulseOutlineImage.pixelsPerUnitMultiplier = PulsePpuRest;
            }

            if (_pulseBackground != null)
            {
                _pulseBackground.sizeDelta = _pulseBackground.sizeDelta.WithY(_bgRestSizeY);
            }

            if (_pulseFillArea != null)
            {
                _pulseFillArea.sizeDelta = _pulseFillArea.sizeDelta.WithY(_fillRestSizeY);
            }
        }
    }
}
