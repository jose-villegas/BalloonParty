using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Score
{
    internal class ScoreController : IStartable, IDisposable
    {
        private const string LevelKey = "Level";
        private const string ProgressSuffix = ".Progress";

        private readonly IGameConfiguration _config;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly ReactiveProperty<int> _level = new(1);
        private readonly Dictionary<string, int> _levelProgress = new();
        private readonly IPublisher<ScoreLevelUpMessage> _levelUpPublisher;
        private readonly GamePalette _palette;
        private readonly Dictionary<string, int> _persistentScore = new();
        private readonly Dictionary<string, int> _projectedProgress = new();
        private readonly IPublisher<ScorePointMessage> _scoredPublisher;
        private readonly ColorStreakTracker _streakTracker;
        private readonly ReactiveProperty<int> _totalScore = new(0);
        private readonly ISubscriber<ScoreTrailArrivedMessage> _trailArrivedSubscriber;
        private IDisposable _subscription;
        private IDisposable _trailSubscription;

        public IReadOnlyReactiveProperty<int> Level => _level;
        public IReadOnlyReactiveProperty<int> TotalScore => _totalScore;

        public ScoreController(
            ISubscriber<ActorHitMessage> hitSubscriber,
            ISubscriber<ScoreTrailArrivedMessage> trailArrivedSubscriber,
            IPublisher<ScorePointMessage> scoredPublisher,
            IPublisher<ScoreLevelUpMessage> levelUpPublisher,
            IGameConfiguration config,
            GamePalette palette,
            ColorStreakTracker streakTracker)
        {
            _hitSubscriber = hitSubscriber;
            _trailArrivedSubscriber = trailArrivedSubscriber;
            _scoredPublisher = scoredPublisher;
            _levelUpPublisher = levelUpPublisher;
            _config = config;
            _palette = palette;
            _streakTracker = streakTracker;
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
                _projectedProgress[color.Name] = _levelProgress[color.Name];
            }

            _totalScore.Value = _persistentScore.Values.Sum();

            _subscription = _hitSubscriber.Subscribe(OnActorHit);
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
        ///     Uses projected (not confirmed) progress so the cinematic can
        ///     register before in-flight trails from other colors arrive.
        /// </summary>
        internal bool WillLevelUp()
        {
            var required = _config.PointsRequiredForLevel(_level.Value + 1);

            foreach (var kvp in _projectedProgress)
            {
                if (kvp.Value < required)
                {
                    return false;
                }
            }

            return true;
        }

        private bool AllColorsConfirmed(int required)
        {
            foreach (var kvp in _levelProgress)
            {
                if (kvp.Value < required)
                {
                    return false;
                }
            }

            return true;
        }

        private void CheckLevelUp()
        {
            var required = _config.PointsRequiredForLevel(_level.Value + 1);
            if (!AllColorsConfirmed(required))
            {
                return;
            }

            _level.Value++;

            foreach (var key in _levelProgress.Keys.ToArray())
            {
                _levelProgress[key] = 0;
                _projectedProgress[key] = 0;
            }

            _levelUpPublisher.Publish(new ScoreLevelUpMessage(_level.Value));
            Navigation.TransitionTo(NavigationState.LevelUp);
        }

        private void OnActorHit(ActorHitMessage msg)
        {
            if (msg.Outcome != HitOutcome.Pop && msg.Outcome != HitOutcome.PassThrough)
            {
                return;
            }

            if (msg.Actor is not IHasScoreColor scoreColor)
            {
                return;
            }

            using var attributionPool = UnityEngine.Pool.ListPool<ScoreAttribution>.Get(out var attributions);
            scoreColor.ResolveScoreAttribution(in msg.Context, attributions);
            PublishAttributionGroup(attributions, msg.WorldPosition);
        }

        /// <summary>
        /// Publishes all attributions from a single <see cref="IHasScoreColor.ResolveScoreAttribution"/>
        /// call as one scatter group. Every message shares the same <c>GroupSize</c> so the UI can
        /// fan them out together regardless of how many colours are involved.
        /// </summary>
        private void PublishAttributionGroup(IList<ScoreAttribution> attributions, Vector3 worldPosition)
        {
            if (attributions.Count == 0)
            {
                return;
            }

            using var resolvedPool = UnityEngine.Pool.ListPool<(string Color, int Points, int BaseProgress)>.Get(out var resolved);

            var multiplier = 1;
            if (attributions.Count == 1)
            {
                multiplier = _streakTracker.Record(attributions[0].ColorId, attributions[0].BreaksStreak);
            }
            else
            {
                _streakTracker.Record(null, breaksStreak: true);
            }

            foreach (var attribution in attributions)
            {
                var color = attribution.ColorId;
                if (string.IsNullOrEmpty(color) || !_persistentScore.ContainsKey(color))
                {
                    continue;
                }

                var pts = attribution.Points * multiplier;
                var baseProgress = _projectedProgress.GetValueOrDefault(color);
                _projectedProgress[color] = baseProgress + pts;
                resolved.Add((color, pts, baseProgress));
            }

            var groupSize = 0;
            foreach (var (_, pts, _) in resolved)
            {
                groupSize += pts;
            }

            if (groupSize <= 0)
            {
                return;
            }

            var required = _config.PointsRequiredForLevel(_level.Value + 1);
            var groupIndex = 0;
            foreach (var (color, points, baseProgress) in resolved)
            {
                for (var i = 0; i < points; i++, groupIndex++)
                {
                    var rawScore = baseProgress + i + 1;
                    var nextLevel = rawScore > required;
                    _scoredPublisher.Publish(new ScorePointMessage(
                        color,
                        worldPosition,
                        nextLevel ? rawScore - required : rawScore,
                        nextLevel ? _level.Value + 1 : _level.Value,
                        nextLevel,
                        groupSize,
                        groupIndex));
                }
            }
        }


        private void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus)
            {
                Save();
            }
        }

        private void OnTrailArrived(ScoreTrailArrivedMessage msg)
        {
            if (!_persistentScore.ContainsKey(msg.ColorName))
            {
                return;
            }

            _persistentScore[msg.ColorName]++;
            _totalScore.Value = _persistentScore.Values.Sum();

            var previous = _levelProgress[msg.ColorName];
            _levelProgress[msg.ColorName] = Math.Max(previous, msg.Score);
            _projectedProgress[msg.ColorName] = Math.Max(_projectedProgress[msg.ColorName], msg.Score);

            CheckLevelUp();
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
    }
}
