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
        private IPendingHealthCharges _pending;
        private LossForecast _forecast;

        [SetUp]
        public void SetUp()
        {
            _hp = new ReactiveProperty<int>(3);
            var health = Substitute.For<IPlayerHealth>();
            health.Current.Returns(_hp);
            _pending = Substitute.For<IPendingHealthCharges>();
            _forecast = new LossForecast(health, _pending);
        }

        [Test]
        public void PendingBelowRemaining_NotImminent()
        {
            _pending.PendingCharges.Returns(2);

            Assert.IsFalse(_forecast.LossImminent);
        }

        [Test]
        public void PendingCoversRemaining_Imminent()
        {
            // The loss is certain at reject-queue time — every queued charge lands unconditionally.
            _pending.PendingCharges.Returns(3);

            Assert.IsTrue(_forecast.LossImminent);
        }

        [Test]
        public void ZeroHp_StaysImminentWithNothingPending()
        {
            _hp.Value = 0;
            _pending.PendingCharges.Returns(0);

            Assert.IsTrue(_forecast.LossImminent);
        }
    }
}
