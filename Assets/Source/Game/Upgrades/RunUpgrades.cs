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
    ///     are the ones held when that level began, which is what a retry keeps. Each card held at a level-up is
    ///     announced with <see cref="UpgradeCardHeldMessage" /> (the card reveal's cue).
    ///     A retry bought with a rewarded ad also gets a bonus card: dealt at the retry's reset (announced with
    ///     <see cref="BonusCardDealtMessage" />), applied on top of the held cards, and dropped when that attempt
    ///     ends, won or lost. A free card (<see cref="IFreeCardGrant" />, the golden balloon's) is held the moment
    ///     it's won (or, while a level-up's hand is on screen, once its pick is held), and kept once its level is
    ///     won; a retry of that level drops it, so the retry starts with the cards held when the level began, as it
    ///     always does.
    /// </summary>
    internal sealed class RunUpgrades : IWeaponStats, IShieldsPerBall, IUpgradeDraft, IBonusCardSource,
        IFreeCardGrant, IRunResettable, IStartable, IDisposable
    {
        // Keep the deals off the generator's and the resolver's streams, which share the level's seed.
        private const int DealSeedSalt = 0x2C1B3C6D;
        private const int BonusSeedSalt = 0x5BD1E995;
        private const int FreeCardSeedSalt = 0x1B873593;

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
        private readonly IPublisher<UpgradeCardHeldMessage> _cardHeldPublisher;
        private readonly Dictionary<IUpgradeCardSettings, int> _stacks = new();
        private readonly List<IUpgradeCardSettings> _offer = new();
        private readonly List<IUpgradeCardSettings> _deck = new();
        // The free cards won on the level being played, which its retry gives back.
        private readonly List<IUpgradeCardSettings> _freeThisLevel = new();
        // Free cards won while a level-up's hand is on screen, dealt once its pick is held: what to call for each
        // if no card is left by then.
        private readonly List<Action> _freeCardsAfterHand = new();
        private readonly ReactiveProperty<int> _shieldsPerBall;
        private readonly Func<IUpgradeCardSettings, float> _dealWeight;

        private IDisposable _phaseSubscription;
        private IUpgradeCardSettings _staged;
        private IUpgradeCardSettings _bonus;
        private int _ballCapBonus;
        private float _reloadScale = 1f;
        private int _shieldBonus;
        private float _extraItemChance;
        private int _limitBonus;
        // Free cards dealt this run, so each deals a different one; a retry winds it back with the cards.
        private int _freeCardsDealt;

        public int MaxBallsInFlight => _flight.MaxBallsInFlight + _ballCapBonus;

        public float ReloadSeconds =>
            Mathf.Max(_flight.ProjectileLoadDuration, _flight.ReloadCooldownSeconds) * _reloadScale;

        public float IdleReloadSeconds => _flight.ProjectileLoadDuration;

        public int StartingShields => _flight.ProjectileStartingShields + _shieldBonus;
        public IReadOnlyReactiveProperty<int> ShieldsPerBall => _shieldsPerBall;
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
                    if (IsDealable(cards[i], forBonus: true, withCapped: false))
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
            IPublisher<BonusCardDealtMessage> bonusDealtPublisher, IPublisher<UpgradeCardHeldMessage> cardHeldPublisher)
        {
            _flight = flight;
            _settings = settings;
            _pacing = pacing;
            _retryState = retryState;
            _levelProgress = levelProgress;
            _bonusDealtPublisher = bonusDealtPublisher;
            _cardHeldPublisher = cardHeldPublisher;
            _shieldsPerBall = new ReactiveProperty<int>(StartingShields);
            _dealWeight = DealWeight;
        }

        public void Start()
        {
            _phaseSubscription = _levelProgress.Phase.Subscribe(OnPhaseChanged);

            // A scene's first run is never a retry, so only ResetRun has to check RetryLevel.
            TakeCheatStartCards();
        }

        public void Dispose()
        {
            _phaseSubscription?.Dispose();
            _shieldsPerBall.Dispose();
        }

        public int StacksOf(IUpgradeCardSettings card)
        {
            return card != null && _stacks.TryGetValue(card, out var count) ? count : 0;
        }

        public int LimitOf(IUpgradeCardSettings card)
        {
            if (card == null)
            {
                return 0;
            }

            return card.Kind == UpgradeKind.RaiseLimits ? card.MaxStacks : card.MaxStacks + _limitBonus;
        }

        /// <summary>
        ///     How likely <paramref name="card" /> is in a deal: its weight, and Raise the Bar's once for every card
        ///     at its limit, so the more cards are capped, the likelier the raise.
        /// </summary>
        internal float DealWeight(IUpgradeCardSettings card)
        {
            return card.Kind == UpgradeKind.RaiseLimits ? card.Weight * CardsAtTheirLimit() : card.Weight;
        }

        // A shot still in flight can win one after the level's last claim. While the hand is on screen, a card
        // now could take one of its cards to its limit under the pick, so it waits for the pick to be held. Once
        // the level advances, the level is won and the card is kept.
        public void GrantFreeCard(Action noCardLeft)
        {
            switch (_levelProgress.Phase.Value)
            {
                case LevelUpPhase.Pending:
                    _freeCardsAfterHand.Add(noCardLeft);
                    break;
                case LevelUpPhase.Transitioning:
                    DealFreeCard(keep: true, noCardLeft);
                    break;
                default:
                    DealFreeCard(keep: false, noCardLeft);
                    break;
            }
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
            GiveBackFreeCards();
            if (_retryState.RetryLevel <= 0)
            {
                _stacks.Clear();
                _freeCardsDealt = 0;
                TakeCheatStartCards();
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

        /// <summary>
        ///     Holds the cards a run that played its way up to <paramref name="startLevel" /> would: every card
        ///     level-up on the way takes its hand's first card, as the level-up does in play, from the same
        ///     seeded deal.
        /// </summary>
        internal void TakeCardsOnTheWayTo(int startLevel)
        {
            for (var level = 2; level <= startLevel; level++)
            {
                OpenHand(level);
                if (_offer.Count > 0)
                {
                    _staged = _offer[0];
                    CommitStaged();
                }
            }

            CloseHand();
            Recompute();
        }

        // A run the start-level cheats begin deep (CheatState.StartLevel) would otherwise play that level with
        // none of the cards a run reaching it holds, which makes it both too weak to clear it in good time and
        // unrepresentative of the level's balance.
        private void TakeCheatStartCards()
        {
#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
            TakeCardsOnTheWayTo(BalloonParty.Cheats.CheatState.StartLevel);
#endif
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
                    // The attempt that earned the bonus is won: the bonus goes, the pick and the free cards stay.
                    _bonus = null;
                    _freeThisLevel.Clear();
                    var held = CommitStaged();
                    CloseHand();
                    Recompute();
                    if (held != null)
                    {
                        _cardHeldPublisher.Publish(new UpgradeCardHeldMessage(held, StacksOf(held)));
                    }

                    DealFreeCardsAfterHand(keep: true);
                    break;
                case LevelUpPhase.Playing:
                    // Reached from Pending only by an aborted level-up: its hand and pick are void, and the level
                    // goes on, so a free card won meanwhile is still this level's.
                    CloseHand();
                    DealFreeCardsAfterHand(keep: false);
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
                var index = _deck.PickWeightedIndex(_dealWeight, (float)rng.NextDouble());
                _offer.Add(_deck[index]);
                _deck.RemoveAt(index);
            }

            _deck.Clear();
        }

        // The card now held, or null when nothing was staged or it was already maxed.
        private IUpgradeCardSettings CommitStaged()
        {
            var card = _staged;
            _staged = null;
            if (card == null || StacksOf(card) >= LimitOf(card))
            {
                return null;
            }

            _stacks[card] = StacksOf(card) + 1;
            Log.Info("Upgrades", $"Holding {card.Title} ({StacksOf(card)}/{LimitOf(card)})");
            return card;
        }

        // Seeded by the retry count too, so each bought retry of the same level can deal a different card.
        // RetriesUsed is read before RetryTracker counts this retry (it resets later), so the run's first
        // retry seeds with 0.
        // A card at its limit stays in the bonus deal: landing on one breaks it on screen (BonusCardDealtMessage.
        // Capped), and the deal goes again among the cards with room, so the ad still buys a card.
        private void DealBonus(int level)
        {
            BuildDeck(forBonus: true, withCapped: true);
            if (_deck.Count == 0)
            {
                return;
            }

            var rng = new System.Random(DealSeed(level, BonusSeedSalt, _retryState.RetriesUsed));
            var pick = _deck[_deck.PickWeightedIndex(_dealWeight, (float)rng.NextDouble())];
            if (StacksOf(pick) >= LimitOf(pick))
            {
                // Only broken on screen when a card with room can take its place.
                BuildDeck(forBonus: true);
                if (_deck.Count == 0)
                {
                    return;
                }

                Log.Info("Upgrades", $"Bonus card {pick.Title} is at its limit; dealing again");
                _bonusDealtPublisher.Publish(new BonusCardDealtMessage(pick, StacksOf(pick), capped: true));
                pick = _deck[_deck.PickWeightedIndex(_dealWeight, (float)rng.NextDouble())];
            }

            _bonus = pick;
            _deck.Clear();

            Log.Info("Upgrades", $"Bonus card for this attempt: {_bonus.Title}");
            _bonusDealtPublisher.Publish(new BonusCardDealtMessage(_bonus, StacksOf(_bonus) + 1));
        }

        // Seeded by the level and the run's free cards so far, so a replay of the level deals the same ones.
        private void DealFreeCard(bool keep, Action noCardLeft)
        {
            BuildDeck(forBonus: false);
            if (_deck.Count == 0)
            {
                noCardLeft?.Invoke();
                return;
            }

            var seed = DealSeed(_levelProgress.Level.Value, FreeCardSeedSalt, _freeCardsDealt);
            var rng = new System.Random(seed);
            var card = _deck[_deck.PickWeightedIndex(_dealWeight, (float)rng.NextDouble())];
            _deck.Clear();

            _stacks[card] = StacksOf(card) + 1;
            _freeCardsDealt++;
            if (!keep)
            {
                _freeThisLevel.Add(card);
            }

            Recompute();
            Log.Info("Upgrades", $"Free card {card.Title} ({StacksOf(card)}/{LimitOf(card)})");
            _cardHeldPublisher.Publish(new UpgradeCardHeldMessage(card, StacksOf(card)));
        }

        private void DealFreeCardsAfterHand(bool keep)
        {
            foreach (var noCardLeft in _freeCardsAfterHand)
            {
                DealFreeCard(keep, noCardLeft);
            }

            _freeCardsAfterHand.Clear();
        }

        private void GiveBackFreeCards()
        {
            _freeCardsAfterHand.Clear();
            _freeCardsDealt -= _freeThisLevel.Count;
            foreach (var card in _freeThisLevel)
            {
                var stacks = StacksOf(card) - 1;
                if (stacks > 0)
                {
                    _stacks[card] = stacks;
                }
                else
                {
                    _stacks.Remove(card);
                }
            }

            _freeThisLevel.Clear();
        }

        private int DealSeed(int level, int salt, int step)
        {
            return unchecked((_pacing.SeedForLevel(level) ^ salt) + step * RetrySeedStep);
        }

        private void CloseHand()
        {
            _offer.Clear();
            _staged = null;
        }

        private void BuildDeck(bool forBonus, bool withCapped = false)
        {
            _deck.Clear();
            var cards = _settings.Cards;
            for (var i = 0; i < cards.Count; i++)
            {
                if (IsDealable(cards[i], forBonus, withCapped))
                {
                    _deck.Add(cards[i]);
                }
            }
        }

        // An ad never buys the ball cap (PLAN-FastPaced §8), so a bonus is never an Extra Pen. A card at its
        // limit is dealt only with withCapped, the bonus deal's first draw, which shows it breaking and deals
        // again. Raise the Bar only matters once a card is at its limit, so it waits for one; and the bonus lasts
        // one attempt, with no room to grow into a raised limit, so it is never the bonus.
        private bool IsDealable(IUpgradeCardSettings card, bool forBonus, bool withCapped)
        {
            if (card == null || card.Weight <= 0f || (!withCapped && StacksOf(card) >= LimitOf(card)))
            {
                return false;
            }

            return card.Kind switch
            {
                UpgradeKind.BallCap => !forBonus,
                UpgradeKind.RaiseLimits => !forBonus && AnyCardAtItsLimit(),
                _ => true
            };
        }

        private bool AnyCardAtItsLimit()
        {
            return CardsAtTheirLimit() > 0;
        }

        private int CardsAtTheirLimit()
        {
            var capped = 0;
            foreach (var (card, count) in _stacks)
            {
                if (card.Kind != UpgradeKind.RaiseLimits && count >= LimitOf(card))
                {
                    capped++;
                }
            }

            return capped;
        }

        private void Recompute()
        {
            _ballCapBonus = 0;
            _reloadScale = 1f;
            _shieldBonus = 0;
            _extraItemChance = 0f;
            _limitBonus = 0;

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
            _shieldsPerBall.Value = StartingShields;
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
                case UpgradeKind.RaiseLimits:
                    _limitBonus += Mathf.RoundToInt(card.Amount);
                    break;
            }
        }
    }
}
