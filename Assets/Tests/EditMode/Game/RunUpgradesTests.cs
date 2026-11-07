using System.Collections.Generic;
using System.Linq;
using BalloonParty.Game.Run;
using BalloonParty.Game.Upgrades;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using NSubstitute;
using NUnit.Framework;

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
    }
}
