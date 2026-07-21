using System;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Health
{
    /// <summary>The player's hit-point pool — the only loss trigger under the spawn-saturation model.</summary>
    internal class PlayerHealthController : IStartable, IRunResettable, IDisposable, IPlayerHealth
    {
        private const int MaxHitPoints = 999;

        private readonly IGameConfiguration _config;
        private readonly ISubscriber<SpawnBlockedMessage> _spawnBlockedSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly IPublisher<EndRunRequestedMessage> _endRunPublisher;
        private readonly ReactiveProperty<int> _current = new();

        private IDisposable _subscription;
        private IDisposable _levelUpSubscription;

        public PlayerHealthController(
            IGameConfiguration config,
            ISubscriber<SpawnBlockedMessage> spawnBlockedSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            IPublisher<EndRunRequestedMessage> endRunPublisher)
        {
            _config = config;
            _spawnBlockedSubscriber = spawnBlockedSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _endRunPublisher = endRunPublisher;
        }

        public IReadOnlyReactiveProperty<int> Current => _current;

        public int ResetOrder => RunResetOrder.Counters;

        public void Start()
        {
            _current.Value = ClampedStartingHitPoints();
            _subscription = _spawnBlockedSubscriber.Subscribe(_ => Damage(1));
            _levelUpSubscription = _levelUpSubscriber.Subscribe(_ => _current.Value = ClampedStartingHitPoints());
        }

        public void ResetRun(int generation)
        {
            _current.Value = ClampedStartingHitPoints();
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _levelUpSubscription?.Dispose();
        }

        private void Damage(int amount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Dev cheat (BlockLevelUpCheat) is a level lock: don't drain hearts while it's on (so the run
            // can't creep toward loss either). RunController.EndRun is also guarded as a backstop.
            if (BalloonParty.Cheats.CheatState.BlockLevelUp)
            {
                return;
            }
#endif

            if (_current.Value <= 0)
            {
                return;
            }

            _current.Value = Mathf.Max(0, _current.Value - amount);
            Log.Info("Health", $"Damage {amount} → {_current.Value} HP remaining (spawn blocked)");

            if (_current.Value == 0)
            {
                // GameOver state gate stops a later blocked spawn from ending the run twice.
                _endRunPublisher.Publish(default);
            }
        }

        private int ClampedStartingHitPoints()
        {
            return Mathf.Clamp(_config.StartingHitPoints, 0, MaxHitPoints);
        }
    }
}
