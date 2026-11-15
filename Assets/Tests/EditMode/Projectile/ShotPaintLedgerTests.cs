using BalloonParty.Balloon.Model;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using NUnit.Framework;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     Which balloons each shot painted itself (the disco ball's wave): an entry lives only while its shot is
    ///     tracked, so a retired shot leaves nothing behind and a shot that is no longer tracked is never booked.
    /// </summary>
    [TestFixture]
    public class ShotPaintLedgerTests
    {
        private ProjectileRegistry _registry;
        private ShotPaintLedger _ledger;

        [SetUp]
        public void SetUp()
        {
            _registry = new ProjectileRegistry();
            _ledger = new ShotPaintLedger(_registry);
        }

        [TearDown]
        public void TearDown()
        {
            _ledger.Dispose();
            _registry.Dispose();
        }

        [Test]
        public void Painted_BalloonRecordedForAShot_IsTheirsAndNoOtherShots()
        {
            var shot = Track();
            var other = Track();
            var balloon = NewBalloon();

            _ledger.Record(shot, balloon);

            Assert.IsTrue(_ledger.Painted(shot, balloon));
            Assert.IsFalse(_ledger.Painted(other, balloon));
            Assert.IsFalse(_ledger.Painted(shot, NewBalloon()));
        }

        [Test]
        public void Record_ShotThatIsNotTracked_IsIgnored()
        {
            var untracked = new ProjectileModel();
            var balloon = NewBalloon();

            _ledger.Record(untracked, balloon);

            Assert.IsFalse(_ledger.Painted(untracked, balloon));
        }

        [Test]
        public void Record_AfterTheShotRetired_IsIgnored()
        {
            var shot = Track();
            _registry.Retire(shot);
            var balloon = NewBalloon();

            _ledger.Record(shot, balloon);

            Assert.IsFalse(_ledger.Painted(shot, balloon));
        }

        [Test]
        public void Retire_DropsThatShotsEntries_AndKeepsTheOthers()
        {
            var retiring = Track();
            var staying = Track();
            var balloon = NewBalloon();
            _ledger.Record(retiring, balloon);
            _ledger.Record(staying, balloon);

            _registry.Retire(retiring);

            Assert.IsFalse(_ledger.Painted(retiring, balloon));
            Assert.IsTrue(_ledger.Painted(staying, balloon));
        }

        [Test]
        public void Retire_ThenANewShotRecords_DoesNotInheritTheOldShotsBalloons()
        {
            var first = Track();
            var oldBalloon = NewBalloon();
            _ledger.Record(first, oldBalloon);
            _registry.Retire(first);

            var second = Track();
            _ledger.Record(second, NewBalloon());

            Assert.IsFalse(_ledger.Painted(second, oldBalloon));
        }

        [Test]
        public void Clear_DropsEveryShotsEntries()
        {
            var shot = Track();
            var balloon = NewBalloon();
            _ledger.Record(shot, balloon);

            _registry.Clear();

            Assert.IsFalse(_ledger.Painted(shot, balloon));
        }

        [Test]
        public void Painted_NullShot_IsFalse()
        {
            Assert.IsFalse(_ledger.Painted(null, NewBalloon()));
        }

        private static BalloonModel NewBalloon()
        {
            return new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
        }

        private ProjectileModel Track()
        {
            var shot = new ProjectileModel();
            _registry.Load(shot, null);
            _registry.Launch(shot);
            return shot;
        }
    }
}
