using System.Collections.Generic;
using System.Linq;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;

namespace BalloonParty.Game
{
    public class ScoreController : IStartable, System.IDisposable
    {
        private const string LevelKey = "Level";
        private const string ProgressSuffix = ".Progress";

        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly IPublisher<BalloonScoredMessage> _scoredPublisher;
        private readonly IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private readonly IGameConfiguration _config;

        private readonly Dictionary<string, int> _persistentScore = new();
        private readonly Dictionary<string, int> _levelProgress = new();
        private System.IDisposable _subscription;

        public ReactiveProperty<int> TotalScore { get; } = new(0);
        public ReactiveProperty<int> Level { get; } = new(0);

        public int GetProgress(string colorName) => _levelProgress.GetValueOrDefault(colorName);
        public int GetRequiredPoints() => _config.PointsRequiredForLevel(Level.Value + 1);

        [Inject]
        public ScoreController(
            ISubscriber<BalloonHitMessage> hitSubscriber,
            IPublisher<BalloonScoredMessage> scoredPublisher,
            IPublisher<ScoreLevelUpMessage> levelUpPublisher,
            IGameConfiguration config)
        {
            _hitSubscriber = hitSubscriber;
            _scoredPublisher = scoredPublisher;
            _levelUpPublisher = levelUpPublisher;
            _config = config;
        }

        public void Start()
        {
            Level.Value = PlayerPrefs.GetInt(LevelKey, 0);

            foreach (var color in _config.BalloonColors)
            {
                _persistentScore[color.Name] = PlayerPrefs.GetInt(color.Name, 0);
                _levelProgress[color.Name] = PlayerPrefs.GetInt(color.Name + ProgressSuffix, 0);
            }

            TotalScore.Value = _persistentScore.Values.Sum();

            _subscription = _hitSubscriber.Subscribe(OnBalloonHit);

            Application.quitting += Save;
            Application.focusChanged += OnFocusChanged;
        }

        public void Dispose()
        {
            Application.quitting -= Save;
            Application.focusChanged -= OnFocusChanged;
            _subscription?.Dispose();
        }

        public void Save()
        {
            foreach (var color in _config.BalloonColors)
            {
                PlayerPrefs.SetInt(color.Name, _persistentScore.GetValueOrDefault(color.Name));
                PlayerPrefs.SetInt(color.Name + ProgressSuffix, _levelProgress.GetValueOrDefault(color.Name));
            }

            PlayerPrefs.SetInt(LevelKey, Level.Value);
            PlayerPrefs.Save();
        }

        private void OnBalloonHit(BalloonHitMessage msg)
        {
            var color = msg.Balloon.Color.Value;
            if (!_persistentScore.ContainsKey(color)) return;

            _persistentScore[color]++;
            _levelProgress[color]++;
            TotalScore.Value = _persistentScore.Values.Sum();

            CheckLevelUp();

            _scoredPublisher.Publish(new BalloonScoredMessage(color, msg.WorldPosition, TotalScore.Value));
        }


        private void CheckLevelUp()
        {
            var required = _config.PointsRequiredForLevel(Level.Value + 1);
            if (_levelProgress.Values.Any(p => p < required)) return;

            Level.Value++;

            foreach (var key in _levelProgress.Keys.ToArray())
                _levelProgress[key] = 0;

            _levelUpPublisher.Publish(new ScoreLevelUpMessage(Level.Value));
            UnityEngine.Time.timeScale = 0f;
        }

        private void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus) Save();
        }
    }
}

