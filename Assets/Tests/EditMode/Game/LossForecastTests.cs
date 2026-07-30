using BalloonParty.Game.Health;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class LossForecastTests
    {
        private ReactiveProperty<int> _hp;
        private LossForecast _forecast;

        [SetUp]
        public void SetUp()
        {
            _hp = new ReactiveProperty<int>(3);
            var health = Substitute.For<IPlayerHealth>();
            health.Current.Returns(_hp);
            _forecast = new LossForecast(health);
        }

        [Test]
        public void PositiveHp_NotImminent()
        {
            Assert.IsFalse(_forecast.LossImminent);
        }

        [Test]
        public void ZeroHp_Imminent()
        {
            _hp.Value = 0;

            Assert.IsTrue(_forecast.LossImminent);
        }
    }
}
