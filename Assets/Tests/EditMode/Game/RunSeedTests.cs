using BalloonParty.Game.Run;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    // NSubstitute returns 0 for IRetryState.RetryLevel by default, so a reset is a fresh run unless a test
    // sets a retry level.
    [TestFixture]
    public class RunSeedTests
    {
        private IRetryState _retry;
        private RunSeed _seed;

        [SetUp]
        public void SetUp()
        {
            _retry = Substitute.For<IRetryState>();
            _seed = new RunSeed(_retry, new System.Random(1));
        }

        [Test]
        public void AFreshRun_RollsANewSeed()
        {
            var first = _seed.Value;

            _seed.ResetRun(1);

            Assert.AreNotEqual(first, _seed.Value);
        }

        [Test]
        public void ARetry_KeepsTheRunsSeed()
        {
            var first = _seed.Value;
            _retry.RetryLevel.Returns(14);

            _seed.ResetRun(1);

            Assert.AreEqual(first, _seed.Value);
        }

        [Test]
        public void ResetOrder_RunsBeforeTheResolverReadsItAndBeforeTheRetryLevelIsCleared()
        {
            // ScenarioController reads it on RunResetMessage, after every reset, so it needs no slot here.
            var resolver = RunResetOrder.Derived;
            var retryTracker = new RetryTracker(Substitute.For<BalloonParty.Shared.IRunConfig>()).ResetOrder;

            Assert.That(_seed.ResetOrder, Is.LessThan(resolver));
            Assert.That(_seed.ResetOrder, Is.LessThan(retryTracker));
        }
    }
}
