using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Upgrades
{
    /// <summary>
    ///     The cards a run holds, the hand a level-up deals, and the weapon the cards add up to. The hand
    ///     follows the level-up phase: it opens on <see cref="LevelUpPhase.Pending" />, its pick is held on
    ///     <see cref="LevelUpPhase.Transitioning" /> (the level has advanced), and anything else voids it.
    ///     So a card is held only once the level that dealt it is completed, and the cards held at a loss
    ///     are the ones held when that level began, which is what a retry keeps.
    ///     A retry bought with a rewarded ad also gets a bonus card: dealt at the retry's reset (announced with
    ///     <see cref="BonusCardDealtMessage" />), applied on top of the held cards, and dropped when that attempt
    ///     ends, won or lost.
    /// </summary>
    internal sealed class RunUpgrades : IWeaponStats, IUpgradeDraft, IBonusCardSource, IRunResettable, IStartable,
        IDisposable
    {
        // Keep the deals off the generator's and the resolver's streams, which share the level's seed.
        private const int DealSeedSalt = 0x2C1B3C6D;
        private const int BonusSeedSalt = 0x5BD1E995;

        // Golden-ratio step: spreads consecutive retry counts across the seed, where XOR-ing a small count
        // in would only flip its low bits.
        private const int RetrySeedStep = unchecked((int)0x9E3779B1);

        // A card can't make the reload instant, however it's authored.
        private const float MinReloadScale = 0.1f;

        private readonly IProjectileFlightConfig _flight;
        private readonly IUpgradeSettings _settings;
        private readonly ILevelPacingConfiguration _pacing;
        private readonly IRetryState _retryState;
        private readonly ILevelProgress _levelProgress;
        private readonly IPublisher<BonusCardDealtMessage> _bonusDealtPublisher;
        private readonly Dictionary<IUpgradeCardSettings, int> _stacks = new();
        private readonly List<IUpgradeCardSettings> _offer = new();
        private readonly List<IUpgradeCardSettings> _deck = new();

        private IDisposable _phaseSubscription;
        private IUpgradeCardSettings _staged;
        private IUpgradeCardSettings _bonus;
        private int _ballCapBonus;
        private float _reloadScale = 1f;
        private int _shieldBonus;
        private float _extraItemChance;

        public int MaxBallsInFlight => _flight.MaxBallsInFlight + _ballCapBonus;

        public float ReloadSeconds =>
            Mathf.Max(_flight.ProjectileLoadDuration, _flight.ReloadCooldownSeconds) * _reloadScale;

        public float IdleReloadSeconds => _flight.ProjectileLoadDuration;

        public int StartingShields => _flight.ProjectileStartingShields + _shieldBonus;
        public float ExtraItemChance => _extraItemChance;
        public IReadOnlyList<IUpgradeCardSettings> Offer => _offer;
        public IUpgradeCardSettings BonusCard => _bonus;

        public bool CanDealBonus
        {
            get
            {
                var cards = _settings.Cards;
                for (var i = 0; i < cards.Count; i++)
                {
                    if (IsDealable(cards[i], forBonus: true))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // Before RetryTracker (Score + 10) consumes the staged retry level this reads.
        public int ResetOrder => RunResetOrder.Counters;

        public RunUpgrades(IProjectileFlightConfig flight, IUpgradeSettings settings, ILevelPacingConfiguration pacing,
            IRetryState retryState, ILevelProgress levelProgress,
            IPublisher<BonusCardDealtMessage> bonusDealtPublisher)
        {
            _flight = flight;
            _settings = settings;
            _pacing = pacing;
            _retryState = retryState;
            _levelProgress = levelProgress;
            _bonusDealtPublisher = bonusDealtPublisher;
        }

        public void Start()
        {
            _phaseSubscription = _levelProgress.Phase.Subscribe(OnPhaseChanged);
        }

        public void Dispose()
        {
            _phaseSubscription?.Dispose();
        }

        public int StacksOf(IUpgradeCardSettings card)
        {
            return card != null && _stacks.TryGetValue(card, out var count) ? count : 0;
        }

        public bool Pick(int slot)
        {
            if (_staged != null || slot < 0 || slot >= _offer.Count)
            {
                return false;
            }

            _staged = _offer[slot];
            return true;
        }

        public void ResetRun(int generation)
        {
            CloseHand();
            _bonus = null;
            if (_retryState.RetryLevel <= 0)
            {
                _stacks.Clear();
            }
            else if (_retryState.RetryWithBonusCard)
            {
                DealBonus(_retryState.RetryLevel);
            }

            Recompute();
        }

        // Counted from level 1, so every N level-ups lands on the same levels whatever level a retry
        // starts from: with N = 2, entering levels 3, 5, 7.
        internal bool IsCardLevel(int newLevel)
        {
            var every = _settings.CardEveryLevels;
            return every > 0 && newLevel > 1 && (newLevel - 1) % every == 0;
        }

        private void OnPhaseChanged(LevelUpPhase phase)
        {
            switch (phase)
            {
                case LevelUpPhase.Pending:
                    // Level is still the one just completed; it advances on dismissal.
                    OpenHand(_levelProgress.Level.Value + 1);
                    break;
                case LevelUpPhase.Transitioning:
                    // The attempt that earned the bonus is won: the bonus goes, the pick stays.
                    _bonus = null;
                    CommitStaged();
                    CloseHand();
                    Recompute();
                    break;
                case LevelUpPhase.Playing:
                    // Reached from Pending only by an aborted level-up: its hand and pick are void.
                    CloseHand();
                    break;
            }
        }

        private void OpenHand(int newLevel)
        {
            CloseHand();
            if (!IsCardLevel(newLevel))
            {
                return;
            }

            BuildDeck(forBonus: false);
            var rng = new System.Random(_pacing.SeedForLevel(newLevel) ^ DealSeedSalt);
            var deal = Mathf.Min(_settings.CardsOffered, _deck.Count);
            for (var i = 0; i < deal; i++)
            {
                // Every deck card has a positive weight, so the pick is never -1.
                var index = _deck.PickWeightedIndex(card => card.Weight, (float)rng.NextDouble());
                _offer.Add(_deck[index]);
                _deck.RemoveAt(index);
            }

            _deck.Clear();
        }

        private void CommitStaged()
        {
            var card = _staged;
            _staged = null;
            if (card == null || StacksOf(card) >= card.MaxStacks)
            {
                return;
            }

            _stacks[card] = StacksOf(card) + 1;
            Log.Info("Upgrades", $"Holding {card.Title} ({StacksOf(card)}/{card.MaxStacks})");
        }

        // Seeded by the retry count too, so each bought retry of the same level can deal a different card.
        // RetriesUsed is read before RetryTracker counts this retry (it resets later), so the run's first
        // retry seeds with 0.
        private void DealBonus(int level)
        {
            BuildDeck(forBonus: true);
            if (_deck.Count == 0)
            {
                return;
            }

            var seed = unchecked((_pacing.SeedForLevel(level) ^ BonusSeedSalt) + _retryState.RetriesUsed * RetrySeedStep);
            var rng = new System.Random(seed);
            _bonus = _deck[_deck.PickWeightedIndex(card => card.Weight, (float)rng.NextDouble())];
            _deck.Clear();

            Log.Info("Upgrades", $"Bonus card for this attempt: {_bonus.Title}");
            _bonusDealtPublisher.Publish(new BonusCardDealtMessage(_bonus));
        }

        private void CloseHand()
        {
            _offer.Clear();
            _staged = null;
        }

        private void BuildDeck(bool forBonus)
        {
            _deck.Clear();
            var cards = _settings.Cards;
            for (var i = 0; i < cards.Count; i++)
            {
                if (IsDealable(cards[i], forBonus))
                {
                    _deck.Add(cards[i]);
                }
            }
        }

        // An ad never buys the ball cap (PLAN-FastPaced §8), so a bonus is never an Extra Ball. A maxed card
        // is out of both deals, so a bonus never takes a card past its max either.
        private bool IsDealable(IUpgradeCardSettings card, bool forBonus)
        {
            return card != null && card.Weight > 0f && StacksOf(card) < card.MaxStacks
                && !(forBonus && card.Kind == UpgradeKind.BallCap);
        }

        private void Recompute()
        {
            _ballCapBonus = 0;
            _reloadScale = 1f;
            _shieldBonus = 0;
            _extraItemChance = 0f;

            foreach (var (card, count) in _stacks)
            {
                for (var i = 0; i < count; i++)
                {
                    Apply(card);
                }
            }

            if (_bonus != null)
            {
                Apply(_bonus);
            }

            _reloadScale = Mathf.Max(MinReloadScale, _reloadScale);
        }

        private void Apply(IUpgradeCardSettings card)
        {
            switch (card.Kind)
            {
                case UpgradeKind.BallCap:
                    _ballCapBonus += Mathf.RoundToInt(card.Amount);
                    break;
                case UpgradeKind.FasterReload:
                    _reloadScale *= 1f - Mathf.Clamp01(card.Amount);
                    break;
                case UpgradeKind.Shield:
                    _shieldBonus += Mathf.RoundToInt(card.Amount);
                    break;
                case UpgradeKind.ItemOdds:
                    _extraItemChance += card.Amount;
                    break;
            }
        }
    }
}
