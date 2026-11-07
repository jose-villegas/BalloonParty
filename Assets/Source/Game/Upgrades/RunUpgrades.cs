using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
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
    /// </summary>
    internal sealed class RunUpgrades : IWeaponStats, IUpgradeDraft, IRunResettable, IStartable, IDisposable
    {
        // Keeps the deal off the generator's and the resolver's streams, which share the level's seed.
        private const int DealSeedSalt = 0x2C1B3C6D;

        // A card can't make the reload instant, however it's authored.
        private const float MinReloadScale = 0.1f;

        private readonly IProjectileFlightConfig _flight;
        private readonly IUpgradeSettings _settings;
        private readonly ILevelPacingConfiguration _pacing;
        private readonly IRetryState _retryState;
        private readonly ILevelProgress _levelProgress;
        private readonly Dictionary<IUpgradeCardSettings, int> _stacks = new();
        private readonly List<IUpgradeCardSettings> _offer = new();
        private readonly List<IUpgradeCardSettings> _deck = new();

        private IDisposable _phaseSubscription;
        private IUpgradeCardSettings _staged;
        private int _ballCapBonus;
        private float _reloadScale = 1f;
        private int _shieldBonus;
        private float _extraItemChance;

        public int MaxBallsInFlight => _flight.MaxBallsInFlight + _ballCapBonus;

        public float ReloadSeconds =>
            Mathf.Max(_flight.ProjectileLoadDuration, _flight.ReloadCooldownSeconds) * _reloadScale;

        public int StartingShields => _flight.ProjectileStartingShields + _shieldBonus;
        public float ExtraItemChance => _extraItemChance;
        public IReadOnlyList<IUpgradeCardSettings> Offer => _offer;

        // Before RetryTracker (Score + 10) consumes the staged retry level this reads.
        public int ResetOrder => RunResetOrder.Counters;

        public RunUpgrades(IProjectileFlightConfig flight, IUpgradeSettings settings, ILevelPacingConfiguration pacing,
            IRetryState retryState, ILevelProgress levelProgress)
        {
            _flight = flight;
            _settings = settings;
            _pacing = pacing;
            _retryState = retryState;
            _levelProgress = levelProgress;
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
            if (_retryState.RetryLevel > 0)
            {
                return;
            }

            _stacks.Clear();
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
                    CommitStaged();
                    CloseHand();
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

            BuildDeck();
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
            Recompute();
            Log.Info("Upgrades", $"Holding {card.Title} ({StacksOf(card)}/{card.MaxStacks})");
        }

        private void CloseHand()
        {
            _offer.Clear();
            _staged = null;
        }

        private void BuildDeck()
        {
            _deck.Clear();
            var cards = _settings.Cards;
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card != null && card.Weight > 0f && StacksOf(card) < card.MaxStacks)
                {
                    _deck.Add(card);
                }
            }
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
