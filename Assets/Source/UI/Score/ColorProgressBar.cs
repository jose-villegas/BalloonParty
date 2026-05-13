using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game;
using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using MessagePipe;
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

        [Header("Visuals")] [SerializeField] private Graphic[] _graphicsToSetColor;

        [Header("Progress")] [SerializeField] private Slider _progressSlider;

        [Header("Feedback")] [SerializeField] private Animator _animator;

        [SerializeField] private ParticleSystem _completionParticleSystem;
        [SerializeField] private ScoreNotice _noticePrefab;
        [SerializeField] private ScorePointTrail _trailPrefab;

        [Inject] private IGameConfiguration _config;
        [Inject] private ISubscriber<BalloonScoredMessage> _scoredSubscriber;
        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        [Inject] private PoolManager _poolManager;

        private readonly List<ScoreNotice> _activeNotices = new();

        private PaletteEntry _colorConfig;
        private int _localCount;
        private string _noticePoolKey;
        private string _trailPoolKey;

        public void Setup(PaletteEntry colorConfig, ScoreController scoreController)
        {
            _colorConfig = colorConfig;
            _noticePoolKey = $"ScoreNotice_{colorConfig.Name}";
            _trailPoolKey = $"ScoreTrail_{colorConfig.Name}";

            foreach (var g in _graphicsToSetColor)
            {
                g.color = colorConfig.Color;
            }

            var required = scoreController.GetRequiredPoints();
            _progressSlider.maxValue = required;
            _progressSlider.value = scoreController.GetProgress(colorConfig.Name);

            _scoredSubscriber.Subscribe(OnBalloonScored).AddTo(this);
            _levelUpSubscriber.Subscribe(OnLevelUp).AddTo(this);
        }

        private void OnBalloonScored(BalloonScoredMessage msg)
        {
            if (msg.ColorName != _colorConfig.Name)
            {
                _localCount = 0;
                return;
            }

            _localCount++;
            _progressSlider.value = Mathf.Min(_progressSlider.value + 1, _progressSlider.maxValue);

            SpawnNotice();
            SpawnTrail(msg.WorldPosition);

            if (_progressSlider.value >= _progressSlider.maxValue)
            {
                _completionParticleSystem.gameObject.SetActive(true);
                _completionParticleSystem.Play();
                _animator.SetBool(CompletedParam, true);
            }
        }

        private void OnLevelUp(ScoreLevelUpMessage msg)
        {
            _localCount = 0;
            _progressSlider.maxValue = _config.PointsRequiredForLevel(msg.NewLevel + 1);
            _progressSlider.value = 0;

            _completionParticleSystem.Stop();
            _completionParticleSystem.gameObject.SetActive(false);
            _animator.SetBool(CompletedParam, false);
        }

        private void SpawnNotice()
        {
            DismissFullyShownNotices();

            var notice = _poolManager.GetOrRegister(_noticePoolKey,
                () => new ScoreNoticePoolChannel(_noticePrefab));

            notice.SetParent(transform);
            _activeNotices.Add(notice);
            notice.Show(_localCount,
                _colorConfig.Color,
                () =>
                {
                    _activeNotices.Remove(notice);
                    _poolManager.Return(_noticePoolKey, notice);
                });
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

        private void SpawnTrail(Vector3 fromWorldPosition)
        {
            var trail = _poolManager.GetOrRegister(_trailPoolKey,
                () => new ScoreTrailPoolChannel(_trailPrefab));

            trail.transform.position = fromWorldPosition;
            trail.transform.localScale = Vector3.one;

            trail.Setup(transform.position,
                _colorConfig.Color,
                _config.ScorePointTraceDuration,
                () =>
                {
                    _animator.SetTrigger(TrailHitTrigger);
                    _poolManager.Return(_trailPoolKey, trail);
                });
        }
    }
}
