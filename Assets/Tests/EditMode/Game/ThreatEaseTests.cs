using BalloonParty.Game.Assist;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ThreatEaseTests
    {
        private IPlayerNeed _need;
        private ThreatEase _ease;

        [SetUp]
        public void SetUp()
        {
            _need = Substitute.For<IPlayerNeed>();
            var settings = Substitute.For<IAssistSettings>();
            settings.ThreatEaseAtFullNeed.Returns(0.5f);
            _ease = new ThreatEase(_need, settings);
        }

        [Test]
        public void Eased_OutOfNeed_KeepsTheChance()
        {
            Assert.AreEqual(0.35f, _ease.Eased(0.35f), 1e-6f);
        }

        [Test]
        public void Eased_ScalesTheChanceDownByTheEaseTimesTheNeed()
        {
            _need.Need.Returns(1f);
            Assert.AreEqual(0.2f, _ease.Eased(0.4f), 1e-6f, "half as likely at full need");

            _need.Need.Returns(0.5f);
            Assert.AreEqual(0.3f, _ease.Eased(0.4f), 1e-6f);
        }

        [Test]
        public void Need_IsClampedToOne()
        {
            _need.Need.Returns(3f);

            Assert.AreEqual(1f, _ease.Need);
            Assert.AreEqual(0.2f, _ease.Eased(0.4f), 1e-6f);
        }
    }
}
