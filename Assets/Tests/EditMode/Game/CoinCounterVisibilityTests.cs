using BalloonParty.Game.Coins;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class CoinCounterVisibilityTests
    {
        private const int CoinsFromLevel = 10;

        private ReactiveProperty<int> _level;
        private ReactiveProperty<int> _balance;
        private ICoinSettings _settings;
        private ILevelProgress _levelProgress;
        private ICoinBalance _coinBalance;
        private CoinCounterVisibility _visibility;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<ICoinSettings>();
            _settings.CoinsFromLevel.Returns(CoinsFromLevel);

            _level = new ReactiveProperty<int>(1);
            _balance = new ReactiveProperty<int>(0);
        }

        [TearDown]
        public void TearDown()
        {
            _visibility?.Dispose();
            _level.Dispose();
            _balance.Dispose();
        }

        [Test]
        public void Initialize_BelowTheLevelWithAnEmptyWallet_IsHidden()
        {
            Create();

            Assert.IsFalse(_visibility.Visible.Value);
        }

        [Test]
        public void Initialize_AlreadyAtTheLevel_IsShown()
        {
            _level.Value = CoinsFromLevel;

            Create();

            Assert.IsTrue(_visibility.Visible.Value);
        }

        [Test]
        public void Initialize_WithABalance_IsShownFromTheStart()
        {
            _balance.Value = 3;

            Create();

            Assert.IsTrue(_visibility.Visible.Value);
        }

        [Test]
        public void LevelReachesTheThreshold_Shows()
        {
            Create();

            _level.Value = CoinsFromLevel - 1;
            Assert.IsFalse(_visibility.Visible.Value);

            _level.Value = CoinsFromLevel;
            Assert.IsTrue(_visibility.Visible.Value);
        }

        [Test]
        public void BalanceBecomesPositive_Shows()
        {
            Create();

            _balance.Value = 1;

            Assert.IsTrue(_visibility.Visible.Value);
        }

        [Test]
        public void LevelDropsBack_StaysShown()
        {
            Create();
            _level.Value = CoinsFromLevel + 2;

            _level.Value = 1;

            Assert.IsTrue(_visibility.Visible.Value);
        }

        [Test]
        public void BalanceSpentToZero_StaysShown()
        {
            Create();
            _balance.Value = 5;

            _balance.Value = 0;

            Assert.IsTrue(_visibility.Visible.Value);
        }

        private void Create()
        {
            var config = Substitute.For<IRunConfig>();
            config.Coins.Returns(_settings);

            _levelProgress = Substitute.For<ILevelProgress>();
            _levelProgress.Level.Returns(_level);

            _coinBalance = Substitute.For<ICoinBalance>();
            _coinBalance.Balance.Returns(_balance);

            _visibility = new CoinCounterVisibility(config, _levelProgress, _coinBalance);
            _visibility.Initialize();
        }
    }
}
