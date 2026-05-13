using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game
{
    public class ScoreController : IStartable, IDisposable
    {
        private const string LevelKey = "Level";
        private const string ProgressSuffix = ".Progress";

        private readonly IGameConfiguration _config;
        private readonly GamePalette _palette;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly ReactiveProperty<int> _level = new(0);
        private readonly Dictionary<string, int> _levelProgress = new();
        private readonly IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private readonly Dictionary<string, int> _persistentScore = new();
        private readonly IPublisher<BalloonScoredMessage> _scoredPublisher;
        private readonly ReactiveProperty<int> _totalScore = new(0);

        private IDisposable _subscription;

        public IReadOnlyReactiveProperty<int> Level => _level;
        public IReadOnlyReactiveProperty<int> TotalScore => _totalScore;

        public ScoreController(
            ISubscriber<BalloonHitMessage> hitSubscriber,
            IPublisher<BalloonScoredMessage> scoredPublisher,
            IPublisher<ScoreLevelUpMessage> levelUpPublisher,
            IGameConfiguration config,
            GamePalette palette)
        {
            _hitSubscriber = hitSubscriber;
            _scoredPublisher = scoredPublisher;
            _levelUpPublisher = levelUpPublisher;
            _config = config;
            _palette = palette;
        }

        public void Dispose()
        {
            Application.quitting -= Save;
            Application.focusChanged -= OnFocusChanged;
            _subscription?.Dispose();
        }

        public void Start()
        {
            _level.Value = PlayerPrefs.GetInt(LevelKey, 0);

            foreach (var color in _palette.Colors)
            {
                _persistentScore[color.Name] = PlayerPrefs.GetInt(color.Name, 0);
                _levelProgress[color.Name] = PlayerPrefs.GetInt(color.Name + ProgressSuffix, 0);
            }

            _totalScore.Value = _persistentScore.Values.Sum();

            _subscription = _hitSubscriber.Subscribe(OnBalloonHit);

            Application.quitting += Save;
            Application.focusChanged += OnFocusChanged;
        }

        public int GetProgress(string colorName)
        {
            return _levelProgress.GetValueOrDefault(colorName);
        }

        public int GetRequiredPoints()
        {
            return _config.PointsRequiredForLevel(Level.Value + 1);
        }

        private void Save()
        {
            foreach (var color in _palette.Colors)
            {
                PlayerPrefs.SetInt(color.Name, _persistentScore.GetValueOrDefault(color.Name));
                PlayerPrefs.SetInt(color.Name + ProgressSuffix, _levelProgress.GetValueOrDefault(color.Name));
            }

            PlayerPrefs.SetInt(LevelKey, _level.Value);
            PlayerPrefs.Save();
        }

        private void OnBalloonHit(BalloonHitMessage msg)
        {
            var color = msg.Balloon.Color.Value;
            if (!_persistentScore.ContainsKey(color))
            {
                return;
            }

            _persistentScore[color]++;
            _levelProgress[color]++;
            _totalScore.Value = _persistentScore.Values.Sum();

            CheckLevelUp();

            _scoredPublisher.Publish(new BalloonScoredMessage(color, msg.WorldPosition, _totalScore.Value));
        }

        private void CheckLevelUp()
        {
            var required = _config.PointsRequiredForLevel(_level.Value + 1);
            if (_levelProgress.Values.Any(p => p < required))
            {
                return;
            }

            _level.Value++;

            foreach (var key in _levelProgress.Keys.ToArray())
            {
                _levelProgress[key] = 0;
            }

            _levelUpPublisher.Publish(new ScoreLevelUpMessage(_level.Value));
            Time.timeScale = 0f;
        }

        private void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                Save();
            }
        }
    }
}
