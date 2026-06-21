using System.Collections.Generic;
using BalloonParty.Configuration;
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

namespace BalloonParty.UI.Score
{
    public class ColorProgressBar : MonoBehaviour, ITrailTarget
    {
        private static readonly int CompletedParam = Animator.StringToHash("Completed");
        private static readonly int TrailHitTrigger = Animator.StringToHash("TrailHit");
#if UNITY_EDITOR
        private static readonly ConfigAssetCache<GamePalette> PaletteCache = new();
#endif

        [Header("Configuration")] [PaletteColorName] [SerializeField]
        private string _colorName;
        [Header("Visuals")] [SerializeField] private Graphic[] _graphicsToSetColor;

        [Header("Progress")] [SerializeField] private Slider _progressSlider;

        [Header("Feedback")] [SerializeField] private Animator _animator;

        [SerializeField] private ParticleSystem _completionParticleSystem;
        [SerializeField] private ProgressNotice _pointNoticePrefab;
        [SerializeField] private ProgressNotice _streakNoticePrefab;

        [Inject] private IGamePalette _palette;
        [Inject] private IGameConfiguration _config;
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

        private readonly List<ProgressNotice> _activeNotices = new();

        private PaletteEntry _colorConfig;
        private string _pointNoticePoolKey;
        private int _stashedMaxValue;
        private string _streakNoticePoolKey;

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
            _pointNoticePoolKey = $"PointNotice_{_colorConfig.Name}";
            _streakNoticePoolKey = $"StreakNotice_{_colorConfig.Name}";

            foreach (var g in _graphicsToSetColor)
            {
                g.color = _colorConfig.Color.WithAlpha(g.color.a);
            }

            var required = _scoreController.GetRequiredPoints();
            _progressSlider.maxValue = required;
            _progressSlider.value = _scoreController.GetProgress(_colorConfig.Name);

            _scoreTrailService.RegisterTarget(_colorConfig.Name, this, _colorConfig.Color);

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
                DismissFullyShownNotices();
                SpawnStreakNotice(streak);
            }
        }

        private void OnLevelUp(ScoreLevelUpMessage msg)
        {
            _stashedMaxValue = _config.PointsRequiredForLevel(msg.NewLevel + 1);
            ClearCompletionVfx();
        }

        private void OnRunReset()
        {
            _progressSlider.maxValue = _scoreController.GetRequiredPoints();
            _progressSlider.value = _scoreController.GetProgress(_colorConfig.Name);
            ClearCompletionVfx();
            DismissAllNotices();
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

            SpawnPointNotice(WorldToAnchoredPosition(msg.WorldPosition));

            if (_progressSlider.value >= _progressSlider.maxValue)
            {
                _completionParticleSystem.gameObject.SetActive(true);
                _completionParticleSystem.Play();
                _animator.SetBool(CompletedParam, true);
            }
        }

        public Vector3 Center
        {
            get
            {
                var rectTransform = (RectTransform)transform;
                return rectTransform.TransformPoint(rectTransform.rect.center);
            }
        }

        public Vector3 RandomPosition()
        {
            var rectTransform = (RectTransform)transform;
            var rect = rectTransform.rect;
            var local = new Vector3(
                Random.Range(rect.xMin, rect.xMax),
                Random.Range(rect.yMin, rect.yMax),
                0f);
            return rectTransform.TransformPoint(local);
        }

        private void DismissFullyShownNotices()
        {
            for (var i = _activeNotices.Count - 1; i >= 0; i--)
            {
                if (_activeNotices[i].IsFullyShown)
                {
                    _activeNotices[i].Dismiss();
                }
            }
        }

        private void DismissAllNotices()
        {
            for (var i = _activeNotices.Count - 1; i >= 0; i--)
            {
                _activeNotices[i].Dismiss();
            }
        }

        private Vector2 WorldToAnchoredPosition(Vector3 worldPosition)
        {
            var rectTransform = (RectTransform)transform;
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPoint,
                null,
                out var localPoint);
            return localPoint;
        }

        private void SpawnStreakNotice(int streak)
        {
            var notice = _poolManager.GetOrRegister(_streakNoticePoolKey,
                () => new SimplePoolChannel<ProgressNotice>(_streakNoticePrefab));

            notice.SetParent(transform);
            notice.SetAnchoredPosition(Vector2.zero);
            _activeNotices.Add(notice);
            notice.Show(streak,
                () =>
                {
                    _activeNotices.Remove(notice);
                    _poolManager.Return(_streakNoticePoolKey, notice);
                },
                _colorConfig.Color);
        }

        private void SpawnPointNotice(Vector2 anchoredPosition)
        {
            var notice = _poolManager.GetOrRegister(_pointNoticePoolKey,
                () => new SimplePoolChannel<ProgressNotice>(_pointNoticePrefab));

            notice.SetParent(transform);
            notice.SetAnchoredPosition(anchoredPosition);
            notice.Show(1,
                () => _poolManager.Return(_pointNoticePoolKey, notice));
        }
    }
}
