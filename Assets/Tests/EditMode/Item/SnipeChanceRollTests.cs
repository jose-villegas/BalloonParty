using BalloonParty.Game.Upgrades;
using BalloonParty.Item.Snipe;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Tests.Game;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     The Sharpshooter card's roll as the thrower loads a ball: it hits with the card's chance, arms the ball
    ///     through the Snipe's own path, and never arms a ball that already pierces.
    /// </summary>
    [TestFixture]
    public class SnipeChanceRollTests
    {
        private IWeaponStats _weapon;
        private ISnipeArmer _armer;
        private IUpgradeSettings _settings;
        private SnipeChanceRoll _roll;
        private int _draws;

        [SetUp]
        public void SetUp()
        {
            _weapon = Substitute.For<IWeaponStats>();
            _armer = Substitute.For<ISnipeArmer>();
            _settings = Substitute.For<IUpgradeSettings>();
            _roll = new SnipeChanceRoll(_weapon, _armer, _settings) { Now = () => 0f };
            _draws = 0;
        }

        [Test]
        public void TryArm_NoChance_NeverArmsNorDraws()
        {
            _weapon.SnipeChance.Returns(0f);
            Draw(0f);
            var ball = new ProjectileModel();

            Assert.IsFalse(_roll.TryArm(ball));
            Assert.AreEqual(0, _draws, "a run without the card spends no draw on it");
            _armer.DidNotReceive().Arm(Arg.Any<IWriteableProjectileModel>());
        }

        [TestCase(0.29f, true)]
        [TestCase(0.3f, false)]
        [TestCase(0.9f, false)]
        public void TryArm_ArmsWhenTheDrawFallsUnderTheChance(float draw, bool armed)
        {
            _weapon.SnipeChance.Returns(0.3f);
            Draw(draw);
            var ball = new ProjectileModel();

            Assert.AreEqual(armed, _roll.TryArm(ball));
            _armer.Received(armed ? 1 : 0).Arm(ball);
        }

        [Test]
        public void TryArm_CertainChance_ArmsEvenOnTheTopDraw()
        {
            // Random.value is inclusive of 1, so a chance of 1 must not depend on the draw.
            _weapon.SnipeChance.Returns(1f);
            Draw(1f);
            var ball = new ProjectileModel();

            Assert.IsTrue(_roll.TryArm(ball));
            _armer.Received(1).Arm(ball);
        }

        [Test]
        public void TryArm_BallAlreadyPiercing_LeavesItAlone()
        {
            _weapon.SnipeChance.Returns(1f);
            var ball = new ProjectileModel();
            ball.IsPiercing.Value = true;

            Assert.IsFalse(_roll.TryArm(ball));
            _armer.DidNotReceive().Arm(Arg.Any<IWriteableProjectileModel>());
        }

        [Test]
        public void TryArm_ReadsTheChanceLive_SoACardPickedMidRunAppliesFromTheNextBall()
        {
            Draw(0.2f);
            _weapon.SnipeChance.Returns(0f);
            Assert.IsFalse(_roll.TryArm(new ProjectileModel()));

            _weapon.SnipeChance.Returns(0.3f);
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()));
        }

        [Test]
        public void TryArm_StackedChance_HitsWhereOneStackWouldMiss()
        {
            // Two stacks of 0.15 from the real card arithmetic: a draw of 0.25 misses one stack, hits two.
            var rig = new UpgradeRig(Substitute.For<IProjectileFlightConfig>());
            var card = UpgradeRig.Card(UpgradeKind.SnipeChance, 0.15f);
            var roll = new SnipeChanceRoll(rig.Upgrades, _armer, rig.Settings)
            {
                Random01 = () => 0.25f,
                Now = () => 0f
            };

            rig.Hold(card);
            Assert.IsFalse(roll.TryArm(new ProjectileModel()));

            rig.Hold(card);
            Assert.IsTrue(roll.TryArm(new ProjectileModel()));
            rig.Upgrades.Dispose();
        }

        [Test]
        public void TryArm_AfterAHit_NoLoadRollsUntilTheCooldownHasPassed()
        {
            _weapon.SnipeChance.Returns(0.5f);
            _settings.SnipeCooldownSeconds.Returns(12f);
            Draw(0f);
            var now = 0f;
            _roll.Now = () => now;

            Assert.IsTrue(_roll.TryArm(new ProjectileModel()));
            var drawsAfterHit = _draws;

            now = 11.9f;
            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "still cooling down");
            Assert.AreEqual(drawsAfterHit, _draws, "a load inside the cooldown spends no draw");

            now = 12f;
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()), "the cooldown is over");
        }

        [Test]
        public void TryArm_AMiss_StartsNoCooldown()
        {
            _weapon.SnipeChance.Returns(0.3f);
            _settings.SnipeCooldownSeconds.Returns(12f);
            var now = 0f;
            _roll.Now = () => now;

            Draw(0.9f);
            Assert.IsFalse(_roll.TryArm(new ProjectileModel()));

            now = 1f;
            Draw(0.1f);
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()), "only a hit starts the wait");
        }

        private void Draw(float value)
        {
            _roll.Random01 = () =>
            {
                _draws++;
                return value;
            };
        }
    }
}
