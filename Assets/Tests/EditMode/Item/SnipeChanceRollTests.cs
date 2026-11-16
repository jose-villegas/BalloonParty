using BalloonParty.Configuration.Level;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Level;
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
        private IPlayerNeed _need;
        private IArmourNeed _armour;
        private IAssistSettings _assist;
        private ILevelParameters _levelNow;
        private IActiveLevelParameters _level;
        private SnipeChanceRoll _roll;
        private int _draws;

        [SetUp]
        public void SetUp()
        {
            _weapon = Substitute.For<IWeaponStats>();
            _armer = Substitute.For<ISnipeArmer>();
            _settings = Substitute.For<IUpgradeSettings>();
            _need = Substitute.For<IPlayerNeed>();
            _armour = Substitute.For<IArmourNeed>();
            var pity = Substitute.For<IPitySettings>();
            pity.GrowthPerMiss.Returns(4f);
            pity.MaxChance.Returns(0.5f);
            _assist = Substitute.For<IAssistSettings>();
            _assist.SnipePity.Returns(pity);
            _settings.PurpleLevelSnipeChanceScale.Returns(3f);
            _levelNow = Substitute.For<ILevelParameters>();
            _level = Substitute.For<IActiveLevelParameters>();
            _level.Current.Returns(_levelNow);
            _roll = new SnipeChanceRoll(_weapon, _armer, _settings, _need, _armour, _assist, _level)
            {
                Now = () => 0f
            };
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

        // A purple level's golden balloons are armoured: the card's chance is tripled there, to crack them.
        [TestCase(false, 0.25f, false)]
        [TestCase(true, 0.25f, true)]
        [TestCase(true, 0.3f, false)]
        public void TryArm_PurpleLevel_ScalesTheCardsChance(bool purple, float draw, bool armed)
        {
            _weapon.SnipeChance.Returns(0.1f);
            _levelNow.IsExtraColorLevel.Returns(purple);
            Draw(draw);

            Assert.AreEqual(armed, _roll.TryArm(new ProjectileModel()));
        }

        [Test]
        public void TryArm_PurpleLevel_WithoutTheCard_StillNeverArms()
        {
            _weapon.SnipeChance.Returns(0f);
            _levelNow.IsExtraColorLevel.Returns(true);
            Draw(0f);

            Assert.IsFalse(_roll.TryArm(new ProjectileModel()));
            Assert.AreEqual(0, _draws);
        }

        [Test]
        public void TryArm_NoLevelYet_RollsTheCardsOwnChance()
        {
            _weapon.SnipeChance.Returns(0.1f);
            _level.Current.Returns((ILevelParameters)null);
            Draw(0.25f);

            Assert.IsFalse(_roll.TryArm(new ProjectileModel()));
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
            var roll = new SnipeChanceRoll(rig.Upgrades, _armer, rig.Settings, _need, _armour, _assist, _level)
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

        [Test]
        public void TryArm_InNeed_EachBallLoadedWithoutASnipe_RaisesTheNextOnesChance()
        {
            // 1% from the card; at full need each miss adds four times that.
            _weapon.SnipeChance.Returns(0.01f);
            _need.Need.Returns(1f);
            Draw(0.08f);

            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "1%");
            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "5%");
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()), "9%");
        }

        [Test]
        public void TryArm_FullHearts_ArmourLeftStanding_RaisesTheNextOnesChance()
        {
            // The board's armour builds pity as trouble does, at its own need: 1%, then 1% x (1 + 4 x 0.5 x 0.5).
            _weapon.SnipeChance.Returns(0.01f);
            _armour.Need.Returns(0.5f);
            Draw(0.025f);

            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "1%");
            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "2%");
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()), "3%");
        }

        [Test]
        public void TryArm_ReadsTheLargerOfThePlayersNeedAndTheArmours()
        {
            _weapon.SnipeChance.Returns(0.01f);
            _need.Need.Returns(1f);
            _armour.Need.Returns(0.4f);
            Draw(0.08f);

            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "1%");
            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "5%: the last heart's need, not the armour's");
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()), "9%");
        }

        [Test]
        public void TryArm_OutOfNeed_StaysAtTheCardsChance()
        {
            _weapon.SnipeChance.Returns(0.01f);
            Draw(0.02f);

            for (var i = 0; i < 20; i++)
            {
                Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "no trouble, no help");
            }
        }

        [Test]
        public void TryArm_PityBuiltInNeed_HelpsOnlyWhileTheNeedLasts()
        {
            _weapon.SnipeChance.Returns(0.01f);
            _need.Need.Returns(1f);
            Draw(0.08f);
            _roll.TryArm(new ProjectileModel());
            _roll.TryArm(new ProjectileModel());

            _need.Need.Returns(0f);
            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "back out of trouble, the card's 1% again");

            _need.Need.Returns(1f);
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()), "the pity was kept, so trouble brings it back");
        }

        [Test]
        public void TryArm_AHit_ClearsThePity()
        {
            _weapon.SnipeChance.Returns(0.01f);
            _need.Need.Returns(1f);
            Draw(0.08f);
            _roll.TryArm(new ProjectileModel());
            _roll.TryArm(new ProjectileModel());
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()));

            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "back to the card's 1%");
        }

        [Test]
        public void ResetRun_ClearsThePityAndTheCooldown()
        {
            _weapon.SnipeChance.Returns(0.01f);
            _settings.SnipeCooldownSeconds.Returns(12f);
            _need.Need.Returns(1f);
            Draw(0.08f);
            _roll.TryArm(new ProjectileModel());
            _roll.TryArm(new ProjectileModel());

            _roll.ResetRun(1);
            Assert.IsFalse(_roll.TryArm(new ProjectileModel()), "a new run starts at the card's 1%");

            Draw(0f);
            _roll.TryArm(new ProjectileModel());
            _roll.ResetRun(2);
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()), "no cooldown carried over from the last run");
        }

        [Test]
        public void GuaranteeNext_ArmsTheNextLoad_WithNoCardAndNoDraw()
        {
            _weapon.SnipeChance.Returns(0f);
            Draw(1f);
            var ball = new ProjectileModel();

            _roll.GuaranteeNext();

            Assert.IsTrue(_roll.TryArm(ball));
            Assert.AreEqual(0, _draws);
            _armer.Received(1).Arm(ball);
        }

        [Test]
        public void GuaranteeNext_ArmsOnlyOnce()
        {
            _weapon.SnipeChance.Returns(0f);
            _roll.GuaranteeNext();
            _roll.TryArm(new ProjectileModel());

            Assert.IsFalse(_roll.TryArm(new ProjectileModel()));
        }

        [Test]
        public void GuaranteeNext_IgnoresTheCooldown()
        {
            _settings.SnipeCooldownSeconds.Returns(8f);
            _weapon.SnipeChance.Returns(1f);
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()));
            _roll.Now = () => 1f;

            _roll.GuaranteeNext();

            Assert.IsTrue(_roll.TryArm(new ProjectileModel()), "1 s into an 8 s cooldown");
        }

        [Test]
        public void GuaranteeNext_APiercingBall_LeavesItForTheNextLoad()
        {
            _weapon.SnipeChance.Returns(0f);
            _roll.GuaranteeNext();
            var piercing = new ProjectileModel();
            piercing.IsPiercing.Value = true;

            Assert.IsFalse(_roll.TryArm(piercing));
            Assert.IsTrue(_roll.TryArm(new ProjectileModel()));
        }

        [Test]
        public void ResetRun_DropsAGuarantee()
        {
            _weapon.SnipeChance.Returns(0f);
            _roll.GuaranteeNext();

            _roll.ResetRun(1);

            Assert.IsFalse(_roll.TryArm(new ProjectileModel()));
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
