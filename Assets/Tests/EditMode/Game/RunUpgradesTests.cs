using System.Collections.Generic;
using System.Linq;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Upgrades;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    /// <summary>
    ///     The run's upgrade cards: what each kind does to the weapon, when a level-up deals cards and
    ///     which, that a pick is held only once its level-up advances the level, and that a retry keeps
    ///     the cards while a fresh run drops them.
    /// </summary>
    [TestFixture]
    public class RunUpgradesTests
    {
        private const int BaseCap = 3;
        private const float BaseCooldown = 0.5f;
        private const int BaseShields = 1;

        private IProjectileFlightConfig _flight;
        private UpgradeRig _rig;
        private IUpgradeCardSettings _ballCap;
        private IUpgradeCardSettings _reload;
        private IUpgradeCardSettings _shield;
        private IUpgradeCardSettings _itemOdds;

        private RunUpgrades Upgrades => _rig.Upgrades;

        [SetUp]
        public void SetUp()
        {
            _flight = Substitute.For<IProjectileFlightConfig>();
            _flight.MaxBallsInFlight.Returns(BaseCap);
            _flight.ReloadCooldownSeconds.Returns(BaseCooldown);
            _flight.ProjectileLoadDuration.Returns(0.2f);
            _flight.ProjectileStartingShields.Returns(BaseShields);

            _rig = new UpgradeRig(_flight);
            _ballCap = UpgradeRig.Card(UpgradeKind.BallCap, 1f);
            _reload = UpgradeRig.Card(UpgradeKind.FasterReload, 0.15f);
            _shield = UpgradeRig.Card(UpgradeKind.Shield, 1f);
            _itemOdds = UpgradeRig.Card(UpgradeKind.ItemOdds, 0.5f);
            _rig.Cards.AddRange(new[] { _ballCap, _reload, _shield, _itemOdds });
        }

        [TearDown]
        public void TearDown()
        {
            Upgrades.Dispose();
        }

        [Test]
        public void NoCards_TheWeaponIsTheConfiguredBase()
        {
            Assert.AreEqual(BaseCap, Upgrades.MaxBallsInFlight);
            Assert.AreEqual(BaseCooldown, Upgrades.ReloadSeconds, 1e-5f, "the cooldown floors the 0.2 s slide");
            Assert.AreEqual(BaseShields, Upgrades.StartingShields);
            Assert.AreEqual(0f, Upgrades.ExtraItemChance);
        }

        [Test]
        public void ReloadSeconds_ASlideLongerThanTheCooldownSetsIt()
        {
            _flight.ProjectileLoadDuration.Returns(0.8f);

            Assert.AreEqual(0.8f, Upgrades.ReloadSeconds, 1e-5f);
        }

        [Test]
        public void Hold_BallCap_RaisesTheCap()
        {
            _rig.Hold(_ballCap);
            _rig.Hold(_ballCap);

            Assert.AreEqual(BaseCap + 2, Upgrades.MaxBallsInFlight);
        }

        [Test]
        public void Hold_FasterReload_StacksMultiplicatively()
        {
            _rig.Hold(_reload);
            _rig.Hold(_reload);

            Assert.AreEqual(BaseCooldown * 0.85f * 0.85f, Upgrades.ReloadSeconds, 1e-5f);
        }

        [Test]
        public void Hold_FasterReload_NeverMakesTheReloadInstant()
        {
            var huge = UpgradeRig.Card(UpgradeKind.FasterReload, 0.95f, maxStacks: 5);
            for (var i = 0; i < 5; i++)
            {
                _rig.Hold(huge);
            }

            Assert.AreEqual(BaseCooldown * 0.1f, Upgrades.ReloadSeconds, 1e-5f);
        }

        [Test]
        public void Hold_Shield_AddsAStartingShieldPerBall()
        {
            _rig.Hold(_shield);

            Assert.AreEqual(BaseShields + 1, Upgrades.StartingShields);
        }

        [Test]
        public void ShieldsPerBall_StartsAtTheConfiguredBase()
        {
            Assert.AreEqual(BaseShields, ((IShieldsPerBall)Upgrades).ShieldsPerBall.Value);
        }

        [Test]
        public void ShieldsPerBall_FollowsAShieldCard_AndAFreshRun()
        {
            // The HUD watches this value, so it has to move when the cards do, not only when read.
            var seen = new List<int>();
            using var subscription = ((IShieldsPerBall)Upgrades).ShieldsPerBall.Subscribe(seen.Add);

            _rig.Hold(_shield);
            _rig.RetryState.RetryLevel.Returns(0);
            Upgrades.ResetRun(1);

            CollectionAssert.AreEqual(new[] { BaseShields, BaseShields + 1, BaseShields }, seen);
        }

        [Test]
        public void Hold_ItemOdds_AddsUpTheChance()
        {
            _rig.Hold(_itemOdds);
            _rig.Hold(_itemOdds);

            Assert.AreEqual(1f, Upgrades.ExtraItemChance, 1e-5f);
        }

        [Test]
        public void Hold_MaxedCard_StopsBeingDealt()
        {
            for (var i = 0; i < 3; i++)
            {
                _rig.Hold(_ballCap);
            }

            CollectionAssert.DoesNotContain(_rig.Present(3), _ballCap);
            Assert.AreEqual(BaseCap + 3, Upgrades.MaxBallsInFlight);
        }

        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        [TestCase(5, true)]
        [TestCase(7, true)]
        public void IsCardLevel_EveryTwoLevelUps(int newLevel, bool expected)
        {
            Assert.AreEqual(expected, Upgrades.IsCardLevel(newLevel));
        }

        [Test]
        public void IsCardLevel_EveryLevelUp_FromTheFirst()
        {
            _rig.Settings.CardEveryLevels.Returns(1);

            Assert.IsTrue(Upgrades.IsCardLevel(2));
            Assert.IsFalse(Upgrades.IsCardLevel(1), "a run starting at level 1 is not a level-up");
        }

        [Test]
        public void IsCardLevel_Zero_NeverDealsCards()
        {
            _rig.Settings.CardEveryLevels.Returns(0);

            Assert.IsFalse(Upgrades.IsCardLevel(3));
        }

        [Test]
        public void Offer_NoLevelUpPending_IsEmpty()
        {
            Assert.IsEmpty(Upgrades.Offer);
        }

        [Test]
        public void Present_NotACardLevel_DealsNothing()
        {
            Assert.IsEmpty(_rig.Present(4), "an empty hand is what tells the popup to pass with no screen");
        }

        [Test]
        public void Present_CardLevel_DealsDistinctCards()
        {
            var offer = _rig.Present(3);

            Assert.AreEqual(3, offer.Count);
            CollectionAssert.AllItemsAreUnique(offer);
        }

        [Test]
        public void Present_LeavesOutMaxedAndZeroWeightCards()
        {
            for (var i = 0; i < 3; i++)
            {
                _rig.Hold(_ballCap);
            }

            _itemOdds.Weight.Returns(0f);

            CollectionAssert.AreEquivalent(new[] { _reload, _shield }, _rig.Present(5), "only two cards are left");
        }

        [Test]
        public void Present_SameLevelUp_DealsTheSameHand()
        {
            var first = _rig.Present(5).ToList();
            _rig.Abort();
            var second = _rig.Present(5).ToList();

            CollectionAssert.AreEqual(first, second, "a retry reaching this level-up must be dealt the same hand");
        }

        [Test]
        public void Present_DealsNoPick()
        {
            _rig.Present(3);

            Assert.AreEqual(BaseCap, Upgrades.MaxBallsInFlight);
            Assert.AreEqual(0, Upgrades.StacksOf(_ballCap));
        }

        [Test]
        public void Pick_IsHeldOnlyOnceTheLevelAdvances()
        {
            var picked = _rig.Present(3)[0];

            Assert.IsTrue(Upgrades.Pick(0));
            Assert.AreEqual(0, Upgrades.StacksOf(picked), "still pending: the level hasn't advanced");

            _rig.Advance();

            Assert.AreEqual(1, Upgrades.StacksOf(picked));
            Assert.IsEmpty(Upgrades.Offer, "the hand closes once its pick is held");
        }

        [Test]
        public void Pick_ThenAborted_IsNeverHeld()
        {
            _rig.Present(3);
            Upgrades.Pick(0);

            _rig.Abort();

            Assert.AreEqual(0, _rig.Cards.Sum(Upgrades.StacksOf), "an aborted level-up grants no card");
            Assert.IsEmpty(Upgrades.Offer);
        }

        [Test]
        public void Pick_AfterAbort_IsRejected()
        {
            _rig.Present(3);
            _rig.Abort();

            Assert.IsFalse(Upgrades.Pick(0));
        }

        [Test]
        public void Pick_ReDetectedAfterAbort_OnePickForTheLevel()
        {
            _rig.Present(3);
            Upgrades.Pick(0);
            _rig.Abort();

            _rig.Present(3);
            Upgrades.Pick(0);
            _rig.Advance();

            Assert.AreEqual(1, _rig.Cards.Sum(Upgrades.StacksOf));
        }

        [Test]
        public void Pick_Twice_KeepsTheFirst()
        {
            var offer = _rig.Present(3).ToList();

            Assert.IsTrue(Upgrades.Pick(0));
            Assert.IsFalse(Upgrades.Pick(1), "one level-up, one card");
            _rig.Advance();

            Assert.AreEqual(1, Upgrades.StacksOf(offer[0]));
            Assert.AreEqual(0, Upgrades.StacksOf(offer[1]));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void Pick_SlotOutsideTheHand_IsRejected(int slot)
        {
            _rig.Present(3);

            Assert.IsFalse(Upgrades.Pick(slot));
        }

        [Test]
        public void Pick_NoHandOpen_IsRejected()
        {
            Assert.IsFalse(Upgrades.Pick(0));
            Assert.IsEmpty(_rig.Present(4));
            Assert.IsFalse(Upgrades.Pick(0), "a level-up that deals no cards takes no pick");
        }

        [Test]
        public void RepeatedLevelUps_NeverStackPastTheMax()
        {
            var picks = 0;
            for (var level = 3; level < 60; level += 2)
            {
                var offer = _rig.Present(level);
                if (offer.Count == 0)
                {
                    _rig.Abort();
                    break;
                }

                foreach (var card in offer)
                {
                    Assert.Less(Upgrades.StacksOf(card), card.MaxStacks, "a maxed card was dealt");
                }

                Upgrades.Pick(0);
                _rig.Advance();
                picks++;
            }

            Assert.AreEqual(_rig.Cards.Count * 3, picks, "every stack of every card is picked once, then the deck is empty");
            Assert.IsEmpty(_rig.Present(61), "nothing left to deal once every card is maxed");
        }

        [Test]
        public void ResetRun_FreshRun_RestoresEveryStat()
        {
            _rig.Hold(_ballCap);
            _rig.Hold(_reload);
            _rig.Hold(_shield);
            _rig.Hold(_itemOdds);
            _rig.RetryState.RetryLevel.Returns(0);

            Upgrades.ResetRun(1);

            Assert.AreEqual(BaseCap, Upgrades.MaxBallsInFlight);
            Assert.AreEqual(BaseCooldown, Upgrades.ReloadSeconds, 1e-5f);
            Assert.AreEqual(BaseShields, Upgrades.StartingShields);
            Assert.AreEqual(0f, Upgrades.ExtraItemChance);
        }

        [Test]
        public void ResetRun_Retry_KeepsTheCards()
        {
            _rig.Hold(_ballCap);
            _rig.Hold(_itemOdds);
            _rig.RetryState.RetryLevel.Returns(4);

            Upgrades.ResetRun(2);

            Assert.AreEqual(BaseCap + 1, Upgrades.MaxBallsInFlight);
            Assert.AreEqual(0.5f, Upgrades.ExtraItemChance, 1e-5f);
        }

        [Test]
        public void TakeCardsOnTheWayTo_HoldsWhatAPlayedRunTakes()
        {
            var played = new UpgradeRig(_flight);
            played.Cards.AddRange(_rig.Cards);
            for (var newLevel = 2; newLevel <= 11; newLevel++)
            {
                played.Present(newLevel);
                played.Upgrades.Pick(0);
                played.Advance();
            }

            Upgrades.TakeCardsOnTheWayTo(11);

            foreach (var card in _rig.Cards)
            {
                Assert.AreEqual(played.Upgrades.StacksOf(card), Upgrades.StacksOf(card), card.Title);
            }

            Assert.AreEqual(5, _rig.Cards.Sum(Upgrades.StacksOf), "levels 3, 5, 7, 9 and 11 deal cards");
            Assert.AreEqual(played.Upgrades.MaxBallsInFlight, Upgrades.MaxBallsInFlight, "the weapon is recomputed");
            played.Upgrades.Dispose();
        }

        [Test]
        public void ResetRun_FreshRunFromACheatStartLevel_HoldsTheCardsOnTheWay()
        {
            _rig.RetryState.RetryLevel.Returns(0);
            BalloonParty.Cheats.CheatState.StartLevel = 7;
            try
            {
                Upgrades.ResetRun(1);
            }
            finally
            {
                BalloonParty.Cheats.CheatState.StartLevel = 1;
            }

            Assert.AreEqual(3, _rig.Cards.Sum(Upgrades.StacksOf), "levels 3, 5 and 7 deal cards");
        }

        [Test]
        public void TakeCardsOnTheWayTo_LevelOneOrBelow_HoldsNothing()
        {
            Upgrades.TakeCardsOnTheWayTo(1);
            Upgrades.TakeCardsOnTheWayTo(0);
            Upgrades.TakeCardsOnTheWayTo(-100);

            Assert.AreEqual(0, _rig.Cards.Sum(Upgrades.StacksOf));
            Assert.IsEmpty(Upgrades.Offer);
        }

        [Test]
        public void ResetRun_RetryWithACheatStartLevel_KeepsOnlyTheHeldCards()
        {
            _rig.Hold(_shield);
            BalloonParty.Cheats.CheatState.StartLevel = 7;
            try
            {
                _rig.Retry(9, withBonusCard: false);
            }
            finally
            {
                BalloonParty.Cheats.CheatState.StartLevel = 1;
            }

            Assert.AreEqual(1, _rig.Cards.Sum(Upgrades.StacksOf), "a retry keeps its cards and takes none on the way");
        }

        [Test]
        public void ResetRun_DropsAnOpenHandAndItsPick()
        {
            _rig.Present(3);
            Upgrades.Pick(0);
            _rig.RetryState.RetryLevel.Returns(3);

            Upgrades.ResetRun(1);

            Assert.IsEmpty(Upgrades.Offer);
            Assert.IsFalse(Upgrades.Pick(0));
        }

        [Test]
        public void LevelUp_HoldingACard_AnnouncesItWithItsStacks()
        {
            _rig.Hold(_shield);
            _rig.CardHeld.Received(1).Publish(Arg.Is<UpgradeCardHeldMessage>(m => m.Card == _shield && m.Stacks == 1));

            _rig.Hold(_shield);
            _rig.CardHeld.Received(1).Publish(Arg.Is<UpgradeCardHeldMessage>(m => m.Card == _shield && m.Stacks == 2));
        }

        [Test]
        public void LevelUp_WithNoPick_AnnouncesNothing()
        {
            _rig.Present(3);
            _rig.Advance();

            _rig.CardHeld.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void TakeCardsOnTheWayTo_AnnouncesNothing()
        {
            Upgrades.TakeCardsOnTheWayTo(7);

            Assert.Greater(_rig.Cards.Sum(Upgrades.StacksOf), 0, "the cheat start holds cards");
            _rig.CardHeld.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void BonusRetry_AnnouncesTheStacksTheAttemptPlaysWith()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);
            _rig.Hold(_shield);

            _rig.Retry(4, withBonusCard: true);

            _rig.BonusDealt.Received(1).Publish(Arg.Is<BonusCardDealtMessage>(m => m.Card == _shield && m.Stacks == 2));
        }

        [Test]
        public void BonusRetry_DealsABonusCardOnTopOfTheHeldOnes()
        {
            _rig.Hold(_shield);

            _rig.Retry(4, withBonusCard: true);

            var bonus = Upgrades.BonusCard;
            Assert.IsNotNull(bonus);
            Assert.AreEqual(1, Upgrades.StacksOf(_shield), "the retry keeps the held card");
            Assert.AreEqual(0, Upgrades.StacksOf(bonus) - (bonus == _shield ? 1 : 0), "a bonus isn't a stack");
            _rig.BonusDealt.Received(1).Publish(Arg.Is<BonusCardDealtMessage>(m => m.Card == bonus));
        }

        [Test]
        public void BonusRetry_AppliesTheBonusToTheWeapon()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);

            _rig.Retry(4, withBonusCard: true);

            Assert.AreSame(_shield, Upgrades.BonusCard);
            Assert.AreEqual(BaseShields + 1, Upgrades.StartingShields);
        }

        [Test]
        public void BonusRetry_NeverDealsAnExtraBall()
        {
            for (var level = 2; level < 40; level++)
            {
                _rig.Retry(level, withBonusCard: true);

                Assert.AreNotSame(_ballCap, Upgrades.BonusCard, $"an ad never buys the ball cap (level {level})");
                Assert.AreEqual(BaseCap, Upgrades.MaxBallsInFlight);
            }
        }

        [Test]
        public void BonusRetry_NeverDealsAMaxedCard()
        {
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1), _reload });
            var shield = _rig.Cards[0];
            _rig.Hold(shield);

            for (var level = 2; level < 20; level++)
            {
                _rig.Retry(level, withBonusCard: true);

                Assert.AreSame(_reload, Upgrades.BonusCard, "a maxed card is out of the bonus deal");
            }
        }

        [Test]
        public void CanDealBonus_OnlyExtraBallOrMaxedCardsLeft_IsFalse()
        {
            _rig.Cards.Clear();
            var shield = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            _rig.Cards.AddRange(new[] { _ballCap, shield });
            Assert.IsTrue(Upgrades.CanDealBonus);

            _rig.Hold(shield);

            Assert.IsFalse(Upgrades.CanDealBonus);
        }

        [Test]
        public void RaiseLimits_IsNotDealtUntilACardIsAtItsLimit()
        {
            var raise = UpgradeRig.Card(UpgradeKind.RaiseLimits, 1f, maxStacks: 2);
            var shield = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { raise, shield, _reload });

            CollectionAssert.DoesNotContain(_rig.Present(3), raise, "nothing to raise yet");
            _rig.Abort();
            _rig.Hold(shield);

            CollectionAssert.Contains(_rig.Present(5), raise);
        }

        [Test]
        public void DealWeight_RaiseLimits_GrowsWithEveryCardAtItsLimit()
        {
            var raise = UpgradeRig.Card(UpgradeKind.RaiseLimits, 1f, maxStacks: 2);
            raise.Weight.Returns(2f);
            var shield = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            var reload = UpgradeRig.Card(UpgradeKind.FasterReload, 1f, maxStacks: 1);
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { raise, shield, reload });

            Assert.AreEqual(0f, Upgrades.DealWeight(raise), "nothing capped, nothing to raise");
            _rig.Hold(shield);
            Assert.AreEqual(2f, Upgrades.DealWeight(raise), "one capped: its own weight");
            _rig.Hold(reload);
            Assert.AreEqual(4f, Upgrades.DealWeight(raise), "two capped: twice as likely");
            Assert.AreEqual(1f, Upgrades.DealWeight(shield), "other cards keep their weight");
        }

        [Test]
        public void Hold_RaiseLimits_LetsACardAtItsLimitBeDealtAndHeldAgain()
        {
            var raise = UpgradeRig.Card(UpgradeKind.RaiseLimits, 1f, maxStacks: 2);
            var shield = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { raise, shield });

            _rig.Hold(shield);
            _rig.Hold(raise);

            Assert.AreEqual(2, Upgrades.LimitOf(shield));
            _rig.Hold(shield);
            Assert.AreEqual(2, Upgrades.StacksOf(shield));
            Assert.AreEqual(BaseShields + 2, Upgrades.StartingShields);
        }

        [Test]
        public void LimitOf_RaiseLimits_NeverRaisesItself()
        {
            var raise = UpgradeRig.Card(UpgradeKind.RaiseLimits, 1f, maxStacks: 2);
            var shield = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { raise, shield });

            _rig.Hold(shield);
            _rig.Hold(raise);

            Assert.AreEqual(2, Upgrades.LimitOf(raise));
            Assert.AreEqual(2, Upgrades.LimitOf(shield));
        }

        [Test]
        public void ResetRun_AFreshRun_DropsTheRaisedLimits()
        {
            var raise = UpgradeRig.Card(UpgradeKind.RaiseLimits, 1f, maxStacks: 2);
            var shield = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { raise, shield });
            _rig.Hold(shield);
            _rig.Hold(raise);

            _rig.RetryState.RetryLevel.Returns(0);
            Upgrades.ResetRun(1);

            Assert.AreEqual(1, Upgrades.LimitOf(shield));
        }

        [Test]
        public void CanDealBonus_OnlyRaiseLimitsLeft_IsFalse()
        {
            var raise = UpgradeRig.Card(UpgradeKind.RaiseLimits, 1f, maxStacks: 2);
            var shield = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { raise, shield });

            _rig.Hold(shield);

            Assert.IsFalse(Upgrades.CanDealBonus, "the ad's card lasts one attempt; a raised limit has no room to fill");
        }

        [Test]
        public void BonusRetry_TheAttemptIsWon_TheBonusGoes()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);
            _rig.Retry(4, withBonusCard: true);

            _rig.Present(5);
            _rig.Advance();

            Assert.IsNull(Upgrades.BonusCard);
            Assert.AreEqual(BaseShields, Upgrades.StartingShields, "the bonus lasted one attempt");
        }

        [Test]
        public void ShieldsPerBall_ABonusShieldCard_RisesForTheAttemptThenDrops()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);
            var seen = new List<int>();
            using var subscription = ((IShieldsPerBall)Upgrades).ShieldsPerBall.Subscribe(seen.Add);

            _rig.Retry(4, withBonusCard: true);
            _rig.Present(5);
            _rig.Advance();

            CollectionAssert.AreEqual(new[] { BaseShields, BaseShields + 1, BaseShields }, seen);
            Assert.AreEqual(Upgrades.StartingShields, ((IShieldsPerBall)Upgrades).ShieldsPerBall.Value);
        }

        [Test]
        public void BonusRetry_TheAttemptIsLostAndRetriedPlain_TheBonusGoes()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);
            _rig.Retry(4, withBonusCard: true);

            _rig.Retry(4, withBonusCard: false);

            Assert.IsNull(Upgrades.BonusCard);
            Assert.AreEqual(BaseShields, Upgrades.StartingShields);
        }

        [Test]
        public void BonusRetry_AnAbortedLevelUp_KeepsTheBonus()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);
            _rig.Retry(4, withBonusCard: true);

            _rig.Present(5);
            _rig.Abort();

            Assert.AreSame(_shield, Upgrades.BonusCard, "the attempt goes on after an abort");
        }

        [Test]
        public void BonusRetry_TheLevelUpCanDealTheSameCard_AndItStacksNormally()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);
            _rig.Settings.CardEveryLevels.Returns(1);
            _rig.Retry(4, withBonusCard: true);

            _rig.Present(5);
            Assert.IsTrue(Upgrades.Pick(0));
            _rig.Advance();

            Assert.AreEqual(1, Upgrades.StacksOf(_shield));
            Assert.AreEqual(BaseShields + 1, Upgrades.StartingShields, "the pick stays, the bonus goes");
        }

        [Test]
        public void CanDealBonus_OnlyZeroWeightAndEmptyEntriesLeft_IsFalse()
        {
            var unweighted = UpgradeRig.Card(UpgradeKind.Shield, 1f);
            unweighted.Weight.Returns(0f);
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { unweighted, null });

            Assert.IsFalse(Upgrades.CanDealBonus, "a zero-weight card is out of every deal");
        }

        [Test]
        public void BonusRetry_NothingDealable_DealsNoBonusAndSaysNothing()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_ballCap);

            _rig.Retry(4, withBonusCard: true);

            Assert.IsNull(Upgrades.BonusCard);
            Assert.AreEqual(BaseCap, Upgrades.MaxBallsInFlight);
            _rig.BonusDealt.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void BonusRetry_ZeroWeightCardsAreSkipped()
        {
            var unweighted = UpgradeRig.Card(UpgradeKind.Shield, 1f);
            unweighted.Weight.Returns(0f);
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { unweighted, _reload });

            for (var level = 2; level < 20; level++)
            {
                _rig.Retry(level, withBonusCard: true);

                Assert.AreSame(_reload, Upgrades.BonusCard);
            }
        }

        [Test]
        public void BonusRetry_LandingOnACappedCard_BreaksIt_ThenDealsOneWithRoom()
        {
            // The shield is at its limit and all but certain to be drawn; the reload has room.
            var capped = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            capped.Weight.Returns(1000f);
            _rig.Cards.Clear();
            _rig.Cards.Add(capped);
            _rig.Hold(capped);
            _rig.Cards.Add(_reload);

            _rig.Retry(4, withBonusCard: true);

            Assert.AreSame(_reload, Upgrades.BonusCard);
            Assert.AreEqual(1, Upgrades.StacksOf(capped), "a capped draw adds nothing");
            Received.InOrder(() =>
            {
                _rig.BonusDealt.Publish(Arg.Is<BonusCardDealtMessage>(m => m.Card == capped && m.Capped && m.Stacks == 1));
                _rig.BonusDealt.Publish(Arg.Is<BonusCardDealtMessage>(m => m.Card == _reload && !m.Capped));
            });
        }

        [Test]
        public void BonusRetry_NeverGivesACappedCard()
        {
            // Weighted to be drawn first nearly every time, so the re-deal is what keeps it out.
            var capped = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            capped.Weight.Returns(1000f);
            _rig.Cards.Clear();
            _rig.Cards.Add(capped);
            _rig.Hold(capped);
            _rig.Cards.Add(_reload);
            _rig.Cards.Add(_itemOdds);

            for (var used = 0; used < 20; used++)
            {
                _rig.RetryState.RetriesUsed.Returns(used);
                _rig.Retry(4, withBonusCard: true);

                Assert.AreNotSame(capped, Upgrades.BonusCard, $"retry {used}");
            }
        }

        [Test]
        public void BonusRetry_WithEveryCardCapped_ShowsNoBreak()
        {
            var capped = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            _rig.Cards.Clear();
            _rig.Cards.Add(capped);
            _rig.Hold(capped);

            _rig.Retry(4, withBonusCard: true);

            Assert.IsNull(Upgrades.BonusCard);
            _rig.BonusDealt.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void BonusRetry_WithRoomOnTheDrawnCard_DealsOnce()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);

            _rig.Retry(4, withBonusCard: true);

            _rig.BonusDealt.Received(1).Publish(Arg.Any<BonusCardDealtMessage>());
            _rig.BonusDealt.DidNotReceive().Publish(Arg.Is<BonusCardDealtMessage>(m => m.Capped));
        }

        [Test]
        public void BonusRetry_SameLevelAndRetryCount_DealsTheSameCard()
        {
            _rig.RetryState.RetriesUsed.Returns(1);
            _rig.Retry(4, withBonusCard: true);
            var first = Upgrades.BonusCard;

            _rig.Retry(4, withBonusCard: true);

            Assert.AreSame(first, Upgrades.BonusCard, "the deal is seeded, not rolled fresh");
        }

        [Test]
        public void BonusRetry_EachBoughtRetryOfTheSameLevel_CanDealADifferentCard()
        {
            var dealt = new HashSet<IUpgradeCardSettings>();

            for (var used = 0; used < 20; used++)
            {
                _rig.RetryState.RetriesUsed.Returns(used);
                _rig.Retry(4, withBonusCard: true);
                dealt.Add(Upgrades.BonusCard);
            }

            Assert.Greater(dealt.Count, 1, "RetriesUsed is part of the seed so a repeat retry isn't a repeat deal");
        }

        [Test]
        public void BonusRetry_AHeldCardOfTheSameKind_StacksWithTheBonusButNotTheCount()
        {
            _rig.Hold(_shield);
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);

            _rig.Retry(4, withBonusCard: true);

            Assert.AreSame(_shield, Upgrades.BonusCard);
            Assert.AreEqual(1, Upgrades.StacksOf(_shield), "the bonus isn't a held stack");
            Assert.AreEqual(BaseShields + 2, Upgrades.StartingShields);
        }

        [Test]
        public void BonusRetry_RepeatedAds_NeverStackPower()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);

            for (var used = 0; used < 4; used++)
            {
                _rig.RetryState.RetriesUsed.Returns(used);
                _rig.Retry(4, withBonusCard: true);
            }

            Assert.AreEqual(BaseShields + 1, Upgrades.StartingShields, "each bonus replaces the last, one card at a time");
            Assert.AreEqual(0, Upgrades.StacksOf(_shield));
        }

        [Test]
        public void BonusRetry_ThenWonThenLostPlain_StaysWithoutTheBonus()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);
            _rig.Retry(4, withBonusCard: true);
            _rig.Present(5);
            _rig.Advance();

            _rig.Retry(5, withBonusCard: false);

            Assert.IsNull(Upgrades.BonusCard);
            Assert.AreEqual(BaseShields, Upgrades.StartingShields);
        }

        [Test]
        public void BonusRetry_FasterReload_ScalesTheReload()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_reload);

            _rig.Retry(4, withBonusCard: true);

            Assert.AreEqual(BaseCooldown * 0.85f, Upgrades.ReloadSeconds, 1e-5f);
        }

        [Test]
        public void BonusRetry_ItemOdds_RaisesTheExtraItemChance()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_itemOdds);

            _rig.Retry(4, withBonusCard: true);

            Assert.AreEqual(0.5f, Upgrades.ExtraItemChance, 1e-5f);
        }

        [Test]
        public void BonusRetry_AFreshRunDropsTheBonus()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_shield);
            _rig.Retry(4, withBonusCard: true);

            _rig.Retry(0, withBonusCard: false);

            Assert.IsNull(Upgrades.BonusCard);
            Assert.AreEqual(BaseShields, Upgrades.StartingShields);
        }

        [Test]
        public void PlainRetry_DealsNoBonusAndSaysNothing()
        {
            _rig.Retry(4, withBonusCard: false);

            Assert.IsNull(Upgrades.BonusCard);
            _rig.BonusDealt.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void BonusRetry_ALevelUpPickedWhileHoldingTheBonus_KeepsThePickAfterTheBonusGoes()
        {
            _rig.Cards.Clear();
            _rig.Cards.AddRange(new[] { _shield, _reload });
            _rig.Settings.CardEveryLevels.Returns(1);
            _rig.Retry(4, withBonusCard: true);
            var hand = _rig.Present(5);
            var picked = hand[0];
            Upgrades.Pick(0);
            _rig.Advance();

            Assert.IsNull(Upgrades.BonusCard);
            Assert.AreEqual(1, Upgrades.StacksOf(picked));
        }

        [Test]
        public void FreshRun_StagedBonusFlagWithoutARetry_DealsNoBonus()
        {
            _rig.RetryState.RetryWithBonusCard.Returns(true);

            Upgrades.ResetRun(1);

            Assert.IsNull(Upgrades.BonusCard);
            _rig.BonusDealt.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void ResetOrder_RunsBeforeTheRetryIsConsumed()
        {
            Assert.Less(Upgrades.ResetOrder, new RetryTracker(Substitute.For<IRunConfig>()).ResetOrder);
        }

        [Test]
        public void PickWeightedIndex_FollowsTheWeights()
        {
            _ballCap.Weight.Returns(1f);
            _reload.Weight.Returns(3f);
            IReadOnlyList<IUpgradeCardSettings> cards = new[] { _ballCap, _reload };

            Assert.AreEqual(0, cards.PickWeightedIndex(card => card.Weight, 0.2f));
            Assert.AreEqual(1, cards.PickWeightedIndex(card => card.Weight, 0.3f));
            Assert.AreEqual(1, cards.PickWeightedIndex(card => card.Weight, 0.999f));
        }

        [Test]
        public void PickWeightedIndex_AllWeightsZero_IsMinusOne()
        {
            _ballCap.Weight.Returns(0f);
            IReadOnlyList<IUpgradeCardSettings> cards = new[] { _ballCap };

            Assert.AreEqual(-1, cards.PickWeightedIndex(card => card.Weight, 0.5f));
        }

        [Test]
        public void GrantFreeCard_HoldsACardNow_AndAnnouncesIt()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_ballCap);

            var noCard = false;
            Upgrades.GrantFreeCard(() => noCard = true);

            Assert.IsFalse(noCard);
            Assert.AreEqual(1, Upgrades.StacksOf(_ballCap));
            Assert.AreEqual(BaseCap + 1, Upgrades.MaxBallsInFlight);
            _rig.CardHeld.Received(1).Publish(Arg.Is<UpgradeCardHeldMessage>(m => m.Card == _ballCap && m.Stacks == 1));
        }

        [Test]
        public void GrantFreeCard_EveryCardAtItsLimit_SaysSo()
        {
            var once = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            _rig.Cards.Clear();
            _rig.Cards.Add(once);
            Upgrades.GrantFreeCard(null);
            _rig.CardHeld.ClearReceivedCalls();

            var noCard = false;
            Upgrades.GrantFreeCard(() => noCard = true);

            Assert.IsTrue(noCard);
            Assert.AreEqual(1, Upgrades.StacksOf(once));
            _rig.CardHeld.DidNotReceiveWithAnyArgs().Publish(default);
        }

        [Test]
        public void GrantFreeCard_ARetryOfItsLevel_StartsWithoutIt()
        {
            _rig.Hold(_shield);
            _rig.Cards.Clear();
            _rig.Cards.Add(_ballCap);
            Upgrades.GrantFreeCard(null);

            _rig.Retry(_rig.Level.Value, withBonusCard: false);

            Assert.AreEqual(0, Upgrades.StacksOf(_ballCap));
            Assert.AreEqual(BaseCap, Upgrades.MaxBallsInFlight);
            Assert.AreEqual(1, Upgrades.StacksOf(_shield), "the cards held when the level began are kept");
        }

        [Test]
        public void GrantFreeCard_OnceItsLevelIsWon_ARetryKeepsIt()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_ballCap);
            Upgrades.GrantFreeCard(null);
            _rig.Present(_rig.Level.Value + 1);
            _rig.Advance();

            _rig.Retry(_rig.Level.Value, withBonusCard: false);

            Assert.AreEqual(1, Upgrades.StacksOf(_ballCap));
        }

        [Test]
        public void GrantFreeCard_AFreshRun_DropsIt()
        {
            Upgrades.GrantFreeCard(null);
            _rig.RetryState.RetryLevel.Returns(0);

            Upgrades.ResetRun(1);

            Assert.AreEqual(BaseCap, Upgrades.MaxBallsInFlight);
            Assert.AreEqual(0f, Upgrades.ExtraItemChance);
            Assert.AreEqual(BaseShields, Upgrades.StartingShields);
        }

        [Test]
        public void GrantFreeCard_WhileTheHandIsUp_WaitsForThePick_ThenIsKept()
        {
            var once = UpgradeRig.Card(UpgradeKind.Shield, 1f, maxStacks: 1);
            _rig.Cards.Clear();
            _rig.Cards.Add(once);
            _rig.Settings.CardEveryLevels.Returns(1);
            var hand = _rig.Present(_rig.Level.Value + 1);
            Assert.AreEqual(1, hand.Count);
            Upgrades.Pick(0);

            var noCard = false;
            Upgrades.GrantFreeCard(() => noCard = true);
            Assert.AreEqual(0, Upgrades.StacksOf(once), "a card now would take the picked one to its limit");
            Assert.IsFalse(noCard, "the pick isn't held yet");

            _rig.Advance();
            Assert.AreEqual(1, Upgrades.StacksOf(once), "the pick is held first");
            Assert.IsTrue(noCard, "the pick took the last card, so the winner gets the fallback");

            _rig.Retry(_rig.Level.Value, withBonusCard: false);
            Assert.AreEqual(1, Upgrades.StacksOf(once));
        }

        [Test]
        public void GrantFreeCard_WhileTheHandIsUp_ThenTheLevelUpAborts_IsThisLevels()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_ballCap);
            _rig.Present(_rig.Level.Value + 1);
            Upgrades.GrantFreeCard(null);

            _rig.Abort();
            Assert.AreEqual(1, Upgrades.StacksOf(_ballCap));

            _rig.Retry(_rig.Level.Value, withBonusCard: false);
            Assert.AreEqual(0, Upgrades.StacksOf(_ballCap));
        }

        [Test]
        public void GrantFreeCard_WhileTheLastShotFlies_IsHeldNow_AndKeptOnceTheLevelIsWon()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_ballCap);
            _rig.Phase.Value = LevelUpPhase.Completing;

            Upgrades.GrantFreeCard(null);
            Assert.AreEqual(1, Upgrades.StacksOf(_ballCap));

            _rig.Present(_rig.Level.Value + 1);
            _rig.Advance();
            _rig.Retry(_rig.Level.Value, withBonusCard: false);
            Assert.AreEqual(1, Upgrades.StacksOf(_ballCap));
        }

        [Test]
        public void GrantFreeCard_AsTheLevelAdvances_IsKept()
        {
            _rig.Cards.Clear();
            _rig.Cards.Add(_ballCap);
            _rig.Level.Value++;
            _rig.Phase.Value = LevelUpPhase.Transitioning;

            Upgrades.GrantFreeCard(null);
            _rig.Phase.Value = LevelUpPhase.Playing;
            _rig.Retry(_rig.Level.Value, withBonusCard: false);

            Assert.AreEqual(1, Upgrades.StacksOf(_ballCap));
        }

        [Test]
        public void GrantFreeCard_TwoOnOneLevel_CanDealDifferentCards()
        {
            var dealt = new HashSet<IUpgradeCardSettings>();
            for (var level = 1; level <= 20 && dealt.Count < 2; level++)
            {
                var rig = new UpgradeRig(_flight);
                rig.Cards.AddRange(_rig.Cards);
                rig.Level.Value = level;
                rig.Upgrades.GrantFreeCard(null);
                var first = rig.Cards.First(card => rig.Upgrades.StacksOf(card) == 1);
                rig.Upgrades.GrantFreeCard(null);
                if (rig.Cards.Any(card => card != first && rig.Upgrades.StacksOf(card) == 1))
                {
                    dealt.Add(first);
                    dealt.Add(rig.Cards.First(card => card != first && rig.Upgrades.StacksOf(card) == 1));
                }

                rig.Upgrades.Dispose();
            }

            Assert.AreEqual(2, dealt.Count, "the second free card on a level isn't stuck repeating the first");
        }

        [Test]
        public void GrantFreeCard_TheSameLevelDealsTheSameCard()
        {
            var first = new UpgradeRig(_flight);
            var second = new UpgradeRig(_flight);
            first.Cards.AddRange(_rig.Cards);
            second.Cards.AddRange(_rig.Cards);
            first.Level.Value = 7;
            second.Level.Value = 7;

            first.Upgrades.GrantFreeCard(null);
            second.Upgrades.GrantFreeCard(null);

            foreach (var card in _rig.Cards)
            {
                Assert.AreEqual(first.Upgrades.StacksOf(card), second.Upgrades.StacksOf(card), card.Title);
            }

            first.Upgrades.Dispose();
            second.Upgrades.Dispose();
        }
    }
}
