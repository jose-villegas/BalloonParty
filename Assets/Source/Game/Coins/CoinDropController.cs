using System;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.Coins
{
    /// <summary>
    ///     Rolls a coin for each pop from <see cref="ICoinSettings.CoinsFromLevel" /> on and publishes
    ///     <see cref="CoinDroppedMessage" /> for each drop; <see cref="CoinCreditor" /> adds it to the wallet.
    ///     The rolls come from a stream seeded by the level and restarted with each attempt, so every attempt
    ///     at a level drops the same way for the same pops.
    /// </summary>
    internal sealed class CoinDropController : IStartable, IDisposable, IRunResettable
    {
        // Keeps the coin stream apart from the level's other seeded streams.
        private const int SeedSalt = 0x2C01D5EE;

        private readonly ICoinSettings _settings;
        private readonly ILevelProgress _levelProgress;
        private readonly ILevelPacingConfiguration _pacing;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly IPublisher<CoinDroppedMessage> _droppedPublisher;
        private readonly CompositeDisposable _subscriptions = new();

        private Random _rolls;

        // After the level is set back for the new attempt.
        public int ResetOrder => RunResetOrder.Score + 15;

        public CoinDropController(
            IRunConfig config,
            ILevelProgress levelProgress,
            ILevelPacingConfiguration pacing,
            ISubscriber<ActorHitMessage> hitSubscriber,
            IPublisher<CoinDroppedMessage> droppedPublisher)
        {
            _settings = config.Coins;
            _levelProgress = levelProgress;
            _pacing = pacing;
            _hitSubscriber = hitSubscriber;
            _droppedPublisher = droppedPublisher;
        }

        public void Start()
        {
            _levelProgress.Level.Subscribe(Reseed).AddTo(_subscriptions);
            _hitSubscriber.Subscribe(OnHit).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // A retry can restart the level the run died on, which leaves Level unchanged, so it reseeds here too.
        public void ResetRun(int generation)
        {
            Reseed(_levelProgress.Level.Value);
        }

        private void Reseed(int level)
        {
            _rolls = new Random(_pacing.SeedForLevel(level) ^ SeedSalt);
        }

        private void OnHit(ActorHitMessage message)
        {
            if ((message.Outcome & HitOutcome.Pop) == 0 || _levelProgress.Level.Value < _settings.CoinsFromLevel)
            {
                return;
            }

            if (_rolls.NextDouble() >= _settings.DropChance)
            {
                return;
            }

            _droppedPublisher.Publish(new CoinDroppedMessage(1, message.WorldPosition));
        }
    }
}
