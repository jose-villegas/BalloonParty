using System;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Health
{
    /// <summary>
    ///     The player's hit-point pool — the only loss trigger under the spawn-saturation model.
    ///     Each <see cref="SpawnBlockedMessage"/> (one un-spawnable balloon) costs one point;
    ///     reaching zero publishes <see cref="EndRunRequestedMessage"/>, which <c>RunController</c>
    ///     routes through its <c>EndRun</c> seam. (A message rather than a direct call keeps this
    ///     controller out of the <c>IRunResettable</c> graph <c>RunController</c> resolves, which
    ///     would otherwise be a DI cycle.) HP starts/resets to
    ///     <see cref="IGameConfiguration.StartingHitPoints"/> and is hard-capped at
    ///     <see cref="MaxHitPoints"/> internally — the UI shows only the current count.
    /// </summary>
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

        // HP teardown has no dependencies; reset alongside the other counters, before respawn.
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
            if (_current.Value <= 0)
            {
                return;
            }

            _current.Value = Mathf.Max(0, _current.Value - amount);

            if (_current.Value == 0)
            {
                // RunController.EndRun no-ops outside Game / during a cinematic, and the GameOver
                // state gate stops a later blocked spawn from ending the run a second time.
                _endRunPublisher.Publish(default);
            }
        }

        private int ClampedStartingHitPoints()
        {
            return Mathf.Clamp(_config.StartingHitPoints, 0, MaxHitPoints);
        }
    }
}
