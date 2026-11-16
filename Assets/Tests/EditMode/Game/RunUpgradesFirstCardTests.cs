using System.Linq;
using BalloonParty.Game.Upgrades;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    /// <summary>
    ///     The run's first card is the deck's dealt-first card (Sharpshooter), whatever deals it: the first level-up's
    ///     hand leads with it, a golden balloon's free card is it, an ad's bonus card lends it. Once the run holds a
    ///     card, every deal is by weight again.
    /// </summary>
    [TestFixture]
    public class RunUpgradesFirstCardTests
    {
        private UpgradeRig _rig;
        private IUpgradeCardSettings _reload;
        private IUpgradeCardSettings _shield;
        private IUpgradeCardSettings _snipe;

        private RunUpgrades Upgrades => _rig.Upgrades;

        [SetUp]
        public void SetUp()
        {
            var flight = Substitute.For<IProjectileFlightConfig>();
            flight.MaxBallsInFlight.Returns(3);
            flight.ReloadCooldownSeconds.Returns(0.5f);
            flight.ProjectileLoadDuration.Returns(0.2f);
            _rig = new UpgradeRig(flight);
            _reload = UpgradeRig.Card(UpgradeKind.FasterReload, 0.15f);
            _shield = UpgradeRig.Card(UpgradeKind.Shield, 1f);
            // All but undealt by weight, so only the dealt-first rule brings it.
            _snipe = UpgradeRig.Card(UpgradeKind.SnipeChance, 0.01f);
            _snipe.Weight.Returns(1e-6f);
            _snipe.DealtFirst.Returns(true);
            _rig.Cards.AddRange(new[] { _reload, _shield, _snipe });
        }

        [TearDown]
        public void TearDown()
        {
            Upgrades.Dispose();
        }

        [Test]
        public void FirstHand_LeadsWithIt_InTheSlotTheLevelUpTakes()
        {
            var offer = _rig.Present(3);

            Assert.AreSame(_snipe, offer[0]);
            Assert.AreEqual(3, offer.Count);
            CollectionAssert.AllItemsAreUnique(offer);
        }

        [Test]
        public void FirstHand_TakingTheFirstSlot_HoldsIt()
        {
            _rig.Present(3);
            Upgrades.Pick(0);
            _rig.Advance();

            Assert.AreEqual(1, Upgrades.StacksOf(_snipe));
        }

        [Test]
        public void OnceACardIsHeld_HandsDealByWeight()
        {
            _rig.Hold(_reload);
            _rig.Settings.CardsOffered.Returns(1);

            for (var level = 3; level <= 21; level += 2)
            {
                Assert.AreNotSame(_snipe, _rig.Present(level).Single(), $"level {level}");
                _rig.Abort();
            }
        }

        [Test]
        public void FreeCard_TheRunsFirst_IsIt()
        {
            Upgrades.GrantFreeCard(null);

            Assert.AreEqual(1, Upgrades.StacksOf(_snipe));
        }

        [Test]
        public void FreeCard_OnceACardIsHeld_DealsByWeight()
        {
            _rig.Hold(_reload);

            Upgrades.GrantFreeCard(null);

            Assert.AreEqual(0, Upgrades.StacksOf(_snipe));
        }

        [Test]
        public void BonusCard_WhileTheRunHoldsNone_LendsIt()
        {
            _rig.Retry(2, withBonusCard: true);

            Assert.AreSame(_snipe, Upgrades.BonusCard);
            Assert.AreEqual(0, Upgrades.StacksOf(_snipe), "a bonus card is lent for the attempt, not held");
        }

        [Test]
        public void NoCardMarked_TheFirstHandDealsByWeight()
        {
            _snipe.DealtFirst.Returns(false);
            _rig.Settings.CardsOffered.Returns(1);

            Assert.AreNotSame(_snipe, _rig.Present(3).Single());
        }

        [Test]
        public void FreshRun_LeadsWithItAgain()
        {
            _rig.Hold(_reload);
            _rig.RetryState.RetryLevel.Returns(0);
            Upgrades.ResetRun(1);

            Assert.AreSame(_snipe, _rig.Present(3)[0]);
        }
    }
}
