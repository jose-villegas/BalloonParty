using BalloonParty.Game.Assist;
using BalloonParty.Game.Danger;
using BalloonParty.Game.Health;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class PlayerNeedTests
    {
        private ReactiveProperty<int> _hearts;
        private ReactiveProperty<float> _danger;
        private IAssistSettings _settings;
        private PlayerNeed _need;

        [SetUp]
        public void SetUp()
        {
            _hearts = new ReactiveProperty<int>(3);
            _danger = new ReactiveProperty<float>(0f);
            var health = Substitute.For<IPlayerHealth>();
            health.Current.Returns(_hearts);
            health.Max.Returns(3);
            var danger = Substitute.For<IDangerLevel>();
            danger.Level.Returns(_danger);
            _settings = Substitute.For<IAssistSettings>();
            _settings.DangerNeedWeight.Returns(1f);
            _need = new PlayerNeed(health, danger, _settings);
        }

        [TestCase(3, 0f)]
        [TestCase(2, 0.5f)]
        [TestCase(1, 1f)]
        [TestCase(4, 0f)]
        public void Hearts_FullIsNoNeed_TheLastIsFull(int hearts, float need)
        {
            _hearts.Value = hearts;

            Assert.AreEqual(need, _need.Need, 1e-6f);
        }

        [Test]
        public void ADangerousBoard_IsNeed_EvenAtFullHearts()
        {
            _danger.Value = 0.8f;

            Assert.AreEqual(0.8f, _need.Need, 1e-6f);
        }

        [Test]
        public void Need_IsTheLargerOfTheHeartsAndTheDanger()
        {
            _hearts.Value = 2;
            _danger.Value = 0.3f;

            Assert.AreEqual(0.5f, _need.Need, 1e-6f);
        }

        [Test]
        public void TheDangerWeight_ScalesTheDangersShare()
        {
            _settings.DangerNeedWeight.Returns(0.5f);
            _danger.Value = 1f;

            Assert.AreEqual(0.5f, _need.Need, 1e-6f);
        }

        [Test]
        public void ARunOfOneHeart_ReadsNoNeedFromIt()
        {
            Assert.AreEqual(0f, PlayerNeed.HeartsNeed(1, 1));
        }
    }
}
