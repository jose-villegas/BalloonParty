using System.Collections.Generic;
using BalloonParty.Game;
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
        [Header("Visuals")] [SerializeField] private Graphic[] _graphicsToSetColor;

        [Header("Progress")] [SerializeField] private Slider _progressSlider;

        [Header("Feedback")] [SerializeField] private Animator _animator;

        [SerializeField] private ParticleSystem _completionParticleSystem;
        [SerializeField] private ScoreNotice _noticePrefab;
        [SerializeField] private ScorePointTrail _trailPrefab;
        private readonly List<ScoreNotice> _notices = new();
        private readonly List<ScorePointTrail> _trails = new();

        private BalloonColorConfiguration _colorConfig;

        [Inject] private IGameConfiguration _config;
        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private int _localCount;
        [Inject] private ISubscriber<BalloonScoredMessage> _scoredSubscriber;

        public void Setup(BalloonColorConfiguration colorConfig, ScoreController scoreController)
        {
            _colorConfig = colorConfig;

            foreach (var g in _graphicsToSetColor)
                g.color = colorConfig.Color;

            var required = scoreController.GetRequiredPoints();
            _progressSlider.maxValue = required;
            _progressSlider.value = scoreController.GetProgress(colorConfig.Name);

            _scoredSubscriber.Subscribe(OnBalloonScored).AddTo(this);
            _levelUpSubscriber.Subscribe(OnLevelUp).AddTo(this);
        }

        private void OnBalloonScored(BalloonScoredMessage msg)
        {
            if (msg.ColorName != _colorConfig.Name) return;

            _localCount++;
            _progressSlider.value = Mathf.Min(_progressSlider.value + 1, _progressSlider.maxValue);

            SpawnNotice();
            SpawnTrail(msg.WorldPosition);

            if (_progressSlider.value >= _progressSlider.maxValue)
            {
                _completionParticleSystem.gameObject.SetActive(true);
                _completionParticleSystem.Play();
                _animator.SetBool("Completed", true);
            }
        }

        private void OnLevelUp(ScoreLevelUpMessage msg)
        {
            _localCount = 0;
            _progressSlider.maxValue = _config.PointsRequiredForLevel(msg.NewLevel + 1);
            _progressSlider.value = 0;

            _completionParticleSystem.Stop();
            _completionParticleSystem.gameObject.SetActive(false);
            _animator.SetBool("Completed", false);
        }

        private void SpawnNotice()
        {
            var notice = FindAvailable(_notices);
            if (notice == null)
            {
                notice = Instantiate(_noticePrefab, transform);
                _notices.Add(notice);
            }

            notice.Show(_localCount, _colorConfig.Color);
        }

        private void SpawnTrail(Vector3 fromWorldPosition)
        {
            var trail = FindAvailable(_trails);
            if (trail == null)
            {
                trail = Instantiate(_trailPrefab, fromWorldPosition, Quaternion.identity);
                _trails.Add(trail);
            }
            else
            {
                trail.transform.position = fromWorldPosition;
                trail.transform.localScale = Vector3.one;
            }

            trail.Setup(transform.position, _colorConfig.Color, _config,
                () => _animator.SetTrigger("TrailHit"));
        }

        private static ScoreNotice FindAvailable(List<ScoreNotice> pool)
        {
            foreach (var item in pool)
                if (item.IsUsable)
                    return item;
            return null;
        }

        private static ScorePointTrail FindAvailable(List<ScorePointTrail> pool)
        {
            foreach (var item in pool)
                if (item.IsUsable)
                    return item;
            return null;
        }
    }
}