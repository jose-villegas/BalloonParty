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
    internal class PlayerHealthController : IStartable, IRunResettable, IDisposable, IPlayerHealth, IPlayerHealing
    {
        private const int MaxHitPoints = 999;

        private readonly IRunConfig _config;
        private readonly ISubscriber<WaveDamageMessage> _waveDamageSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly IPublisher<EndRunRequestedMessage> _endRunPublisher;
        private readonly ReactiveProperty<int> _current = new();

        private IDisposable _subscription;
        private IDisposable _levelUpSubscription;

        public PlayerHealthController(
            IRunConfig config,
            ISubscriber<WaveDamageMessage> waveDamageSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            IPublisher<EndRunRequestedMessage> endRunPublisher)
        {
            _config = config;
            _waveDamageSubscriber = waveDamageSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _endRunPublisher = endRunPublisher;
        }

        public IReadOnlyReactiveProperty<int> Current => _current;

        public int Max => ClampedStartingHitPoints();

        public int ResetOrder => RunResetOrder.Counters;

        public void Start()
        {
            _current.Value = ClampedStartingHitPoints();
            _subscription = _waveDamageSubscriber.Subscribe(msg => Damage(msg.HeartsLost));
            // A partial refill lets hearts lost in one level carry into the next, so a level-up stops wiping
            // the slate and a player who keeps losing hearts runs out over several levels.
            _levelUpSubscription = _levelUpSubscriber.Subscribe(_ => Heal(_config.LevelUpHeartRefill));
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

        public void Heal(int amount)
        {
            // A pop can land after the killing hit; a heal never brings the player back.
            if (_current.Value <= 0)
            {
                return;
            }

            // Extra hearts (HealPastMax) stay: a heal only tops up to the maximum, so a level-up refill never trims them.
            var max = ClampedStartingHitPoints();
            if (_current.Value >= max)
            {
                return;
            }

            _current.Value = Mathf.Min(max, _current.Value + Mathf.Max(0, amount));
        }

        public void HealPastMax(int amount)
        {
            if (_current.Value <= 0)
            {
                return;
            }

            _current.Value = Mathf.Min(MaxHitPoints, _current.Value + Mathf.Max(0, amount));
        }

        private void Damage(int amount)
        {
#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
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
