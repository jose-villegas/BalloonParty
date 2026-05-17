using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game
{
    internal class ScoreController : IStartable, IDisposable
    {
        private const string LevelKey = "Level";
        private const string ProgressSuffix = ".Progress";

        private readonly IGameConfiguration _config;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private readonly ReactiveProperty<int> _level = new(1);
        private readonly Dictionary<string, int> _levelProgress = new();
        private readonly IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private readonly GamePalette _palette;
        private readonly Dictionary<string, int> _persistentScore = new();
        private readonly Dictionary<string, int> _pendingPoints = new();
        private readonly IPublisher<BalloonScoredMessage> _scoredPublisher;
        private readonly ReactiveProperty<int> _totalScore = new(0);

        private IDisposable _subscription;
        private IDisposable _trailSubscription;

        public IReadOnlyReactiveProperty<int> Level => _level;
        public IReadOnlyReactiveProperty<int> TotalScore => _totalScore;

        public ScoreController(
            ISubscriber<BalloonHitMessage> hitSubscriber,
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            IPublisher<BalloonScoredMessage> scoredPublisher,
            IPublisher<ScoreLevelUpMessage> levelUpPublisher,
            IGameConfiguration config,
            GamePalette palette)
        {
            _hitSubscriber = hitSubscriber;
            _trailArrivedSubscriber = trailArrivedSubscriber;
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
            _trailSubscription?.Dispose();
        }

        public void Start()
        {
            _level.Value = PlayerPrefs.GetInt(LevelKey, 1);

            foreach (var color in _palette.Colors)
            {
                _persistentScore[color.Name] = PlayerPrefs.GetInt(color.Name, 0);
                _levelProgress[color.Name] = PlayerPrefs.GetInt(color.Name + ProgressSuffix, 0);
            }

            _totalScore.Value = _persistentScore.Values.Sum();

            _subscription = _hitSubscriber.Subscribe(OnBalloonHit);
            _trailSubscription = _trailArrivedSubscriber.Subscribe(OnTrailArrived);

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

        /// <summary>
        ///     Predicts whether adding <paramref name="points" /> to the given color
        ///     would complete the level, accounting for in-flight trails that have
        ///     been scored but not yet arrived.
        /// </summary>
        internal bool WillLevelUp(string colorName, int points)
        {
            var required = _config.PointsRequiredForLevel(_level.Value + 1);

            foreach (var kvp in _levelProgress)
            {
                var projected = kvp.Value + _pendingPoints.GetValueOrDefault(kvp.Key);
                if (kvp.Key == colorName)
                {
                    projected += points;
                }

                if (projected < required)
                {
                    return false;
                }
            }

            return true;
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
            if (msg.Balloon.EvaluateHit(msg.Damage) != HitOutcome.Pop)
            {
                return;
            }

            var color = msg.Balloon.Color.Value;
            if (string.IsNullOrEmpty(color) || !_persistentScore.ContainsKey(color))
            {
                return;
            }

            var points = msg.Balloon.ScoreValue;

            _scoredPublisher.Publish(new BalloonScoredMessage(color, msg.WorldPosition, points));
            _pendingPoints[color] = _pendingPoints.GetValueOrDefault(color) + points;
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_persistentScore.ContainsKey(msg.ColorName))
            {
                return;
            }

            _pendingPoints[msg.ColorName] = Math.Max(0,
                _pendingPoints.GetValueOrDefault(msg.ColorName) - 1);

            _persistentScore[msg.ColorName]++;
            _totalScore.Value = _persistentScore.Values.Sum();

            _levelProgress[msg.ColorName]++;
            CheckLevelUp();
        }

        private void CheckLevelUp()
        {
            var required = _config.PointsRequiredForLevel(_level.Value + 1);
            if (!AllColorsComplete(required))
            {
                return;
            }

            _level.Value++;

            foreach (var key in _levelProgress.Keys.ToArray())
            {
                _levelProgress[key] = 0;
            }

            _levelUpPublisher.Publish(new ScoreLevelUpMessage(_level.Value));
            Navigation.TransitionTo(NavigationState.LevelUp);
            Time.timeScale = 0f;
        }

        private bool AllColorsComplete(int required, string extraColor = null, int extraPoints = 0)
        {
            foreach (var kvp in _levelProgress)
            {
                var projected = kvp.Value;
                if (kvp.Key == extraColor)
                {
                    projected += extraPoints;
                }

                if (projected < required)
                {
                    return false;
                }
            }

            return true;
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
