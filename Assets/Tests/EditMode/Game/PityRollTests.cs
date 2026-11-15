using BalloonParty.Game.Assist;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class PityRollTests
    {
        private IPitySettings _settings;
        private PityRoll _roll;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<IPitySettings>();
            _settings.GrowthPerMiss.Returns(4f);
            _settings.MaxChance.Returns(0.5f);
            _roll = new PityRoll(_settings);
        }

        [Test]
        public void MissesInFullNeed_RaiseTheChance_ByTheGrowthTimesTheBase()
        {
            Assert.AreEqual(0.01f, _roll.ChanceFor(0.01f, 1f), 1e-6f);

            _roll.Roll(0.01f, 1f, 0.99f);
            Assert.AreEqual(0.05f, _roll.ChanceFor(0.01f, 1f), 1e-6f);

            _roll.Roll(0.01f, 1f, 0.99f);
            Assert.AreEqual(0.09f, _roll.ChanceFor(0.01f, 1f), 1e-6f);
        }

        [Test]
        public void AMissInHalfNeed_CountsHalf_AndTheBoostScalesWithTheNeedNow()
        {
            _roll.Roll(0.01f, 0.5f, 0.99f);
            _roll.Roll(0.01f, 0.5f, 0.99f);

            Assert.AreEqual(1f, _roll.Pity, 1e-6f);
            Assert.AreEqual(0.05f, _roll.ChanceFor(0.01f, 1f), 1e-6f);
            Assert.AreEqual(0.03f, _roll.ChanceFor(0.01f, 0.5f), 1e-6f);
            Assert.AreEqual(0.01f, _roll.ChanceFor(0.01f, 0f), 1e-6f, "out of need, the base");
        }

        [Test]
        public void MissesOutOfNeed_BuildNoPity()
        {
            for (var i = 0; i < 50; i++)
            {
                _roll.Roll(0.01f, 0f, 0.99f);
            }

            Assert.AreEqual(0f, _roll.Pity);
        }

        [Test]
        public void TheChance_StopsAtTheMax_ButABaseAboveItIsKept()
        {
            for (var i = 0; i < 50; i++)
            {
                _roll.Roll(0.01f, 1f, 0.99f);
            }

            Assert.AreEqual(0.5f, _roll.ChanceFor(0.01f, 1f), 1e-6f);
            Assert.AreEqual(0.8f, _roll.ChanceFor(0.8f, 1f), 1e-6f);
        }

        [Test]
        public void AHit_ClearsThePity()
        {
            _roll.Roll(0.01f, 1f, 0.99f);

            Assert.IsTrue(_roll.Roll(0.01f, 1f, 0.04f));
            Assert.AreEqual(0f, _roll.Pity);
        }

        [Test]
        public void NoBaseChance_NeverHits_NorBuildsPity()
        {
            Assert.IsFalse(_roll.Roll(0f, 1f, 0f));
            Assert.AreEqual(0f, _roll.Pity);
        }

        [Test]
        public void ACertainChance_HitsOnTheTopDraw()
        {
            Assert.IsTrue(_roll.Roll(1f, 0f, 1f));
        }

        [Test]
        public void NoGrowth_IsAPlainRoll()
        {
            _settings.GrowthPerMiss.Returns(0f);
            _roll.Roll(0.01f, 1f, 0.99f);

            Assert.AreEqual(0.01f, _roll.ChanceFor(0.01f, 1f), 1e-6f);
        }
    }
}
