using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration;
using BalloonParty.Game;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using NaughtyAttributes;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI.Score
{
    public class ColorProgressBar : MonoBehaviour
    {
        private static readonly int CompletedParam = Animator.StringToHash("Completed");
        private static readonly int TrailHitTrigger = Animator.StringToHash("TrailHit");

        [Header("Configuration")] [PaletteColorName] [SerializeField]
        private string _colorName;
        [Header("Visuals")] [SerializeField] private Graphic[] _graphicsToSetColor;

        [Header("Progress")] [SerializeField] private Slider _progressSlider;

        [Header("Feedback")] [SerializeField] private Animator _animator;

        [SerializeField] private ParticleSystem _completionParticleSystem;
        [SerializeField] private ScoreNotice _noticePrefab;

        [Inject] private GamePalette _palette;
        [Inject] private IGameConfiguration _config;
        [Inject] private ISubscriber<BalloonScoredMessage> _scoredSubscriber;
        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        [Inject] private ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        [Inject] private PoolManager _poolManager;
        [Inject] private ScoreController _scoreController;
        [Inject] private ScoreTrailService _scoreTrailService;

        private readonly List<ScoreNotice> _activeNotices = new();

        private PaletteEntry _colorConfig;
        private string _noticePoolKey;
        private int _streak;


        private void OnValidate()
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(_colorName) || _graphicsToSetColor == null)
            {
                return;
            }

            var guids = UnityEditor.AssetDatabase.FindAssets("t:GamePalette");
            if (guids.Length == 0)
            {
                return;
            }

            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            var palette = UnityEditor.AssetDatabase.LoadAssetAtPath<GamePalette>(path);
            var entry = System.Array.Find(palette.Colors, c => c.Name == _colorName);
            if (entry == null)
            {
                return;
            }

            foreach (var g in _graphicsToSetColor)
            {
                if (g != null)
                {
                    var c = entry.Color;
                    g.color = new Color(c.r, c.g, c.b, g.color.a);
                }
            }
#endif
        }

        private void Start()
        {
            _colorConfig = _palette.Colors.First(c => c.Name == _colorName);
            _noticePoolKey = $"ScoreNotice_{_colorConfig.Name}";

            foreach (var g in _graphicsToSetColor)
            {
                var c = _colorConfig.Color;
                g.color = new Color(c.r, c.g, c.b, g.color.a);
            }

            var required = _scoreController.GetRequiredPoints();
            _progressSlider.maxValue = required;
            _progressSlider.value = _scoreController.GetProgress(_colorConfig.Name);

            _scoreTrailService.RegisterTarget(_colorConfig.Name, RandomWorldPositionInRect, _colorConfig.Color);

            _scoredSubscriber.Subscribe(OnBalloonScored).AddTo(this);
            _levelUpSubscriber.Subscribe(OnLevelUp).AddTo(this);
            _trailArrivedSubscriber.Subscribe(OnTrailArrived).AddTo(this);
        }

        private void OnBalloonScored(BalloonScoredMessage msg)
        {
            if (msg.ColorName != _colorConfig.Name)
            {
                _streak = 0;
                return;
            }

            _streak++;
            _progressSlider.value = Mathf.Min(_progressSlider.value + msg.Points, _progressSlider.maxValue);

            SpawnNotice(_streak, msg.Points);

            if (_progressSlider.value >= _progressSlider.maxValue)
            {
                _completionParticleSystem.gameObject.SetActive(true);
                _completionParticleSystem.Play();
                _animator.SetBool(CompletedParam, true);
            }
        }

        private void OnLevelUp(ScoreLevelUpMessage msg)
        {
            _streak = 0;
            _progressSlider.maxValue = _config.PointsRequiredForLevel(msg.NewLevel + 1);
            _progressSlider.value = 0;

            _completionParticleSystem.Stop();
            _completionParticleSystem.gameObject.SetActive(false);
            _animator.SetBool(CompletedParam, false);
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (msg.ColorName == _colorConfig.Name)
            {
                _animator.SetTrigger(TrailHitTrigger);
            }
        }

        private void SpawnNotice(int streak, int points)
        {
            if (points <= 1)
            {
                DismissFullyShownNotices();
                SpawnSingleNotice(streak, Vector2.zero);
                return;
            }

            SpawnScatteredNoticesAsync(points).Forget();
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

        private Vector2 RandomPositionInRect()
        {
            var rect = ((RectTransform)transform).rect;
            return new Vector2(
                Random.Range(rect.xMin, rect.xMax),
                Random.Range(2f * rect.yMin, 2f * rect.yMax));
        }

        private Vector3 RandomWorldPositionInRect()
        {
            var rectTransform = (RectTransform)transform;
            var rect = rectTransform.rect;
            var local = new Vector3(
                Random.Range(rect.xMin, rect.xMax),
                Random.Range(rect.yMin, rect.yMax),
                0f);
            return rectTransform.TransformPoint(local);
        }

        private void SpawnSingleNotice(int score, Vector2 anchoredPosition)
        {
            var notice = _poolManager.GetOrRegister(_noticePoolKey,
                () => new ScoreNoticePoolChannel(_noticePrefab));

            notice.SetParent(transform);
            notice.SetAnchoredPosition(anchoredPosition);
            _activeNotices.Add(notice);
            notice.Show(score,
                _colorConfig.Color,
                () =>
                {
                    _activeNotices.Remove(notice);
                    _poolManager.Return(_noticePoolKey, notice);
                });
        }

        private void SpawnUntrackedNotice(Vector2 anchoredPosition)
        {
            var notice = _poolManager.GetOrRegister(_noticePoolKey,
                () => new ScoreNoticePoolChannel(_noticePrefab));

            notice.SetParent(transform);
            notice.SetAnchoredPosition(anchoredPosition);
            notice.Show(1,
                _colorConfig.Color,
                () => _poolManager.Return(_noticePoolKey, notice));
        }


        private async UniTaskVoid SpawnScatteredNoticesAsync(int count)
        {
            var delayMs = Mathf.RoundToInt(3f * _config.ScorePointsScatterDelay * 1000f);
            for (var i = 0; i < count; i++)
            {
                SpawnUntrackedNotice(RandomPositionInRect());
                if (i < count - 1)
                {
                    await UniTask.Delay(delayMs, cancellationToken: destroyCancellationToken);
                }
            }
        }
    }
}
