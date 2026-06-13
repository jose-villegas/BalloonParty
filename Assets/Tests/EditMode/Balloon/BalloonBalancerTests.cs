using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class BalloonBalancerTests
    {
        [Test]
        public void RunScheduledBalance_WithCurrentGeneration_Runs()
        {
            var balancer = CreateBalancer();

            // Empty grid → Balance() is a harmless no-op; we only assert the guard let it run.
            Assert.IsTrue(balancer.RunScheduledBalance(balancer.Generation));
        }

        [Test]
        public void RunScheduledBalance_AfterReset_WithStaleGeneration_DoesNotRun()
        {
            var balancer = CreateBalancer();
            var staleGeneration = balancer.Generation;

            balancer.ResetRun(staleGeneration + 1);

            Assert.IsFalse(balancer.RunScheduledBalance(staleGeneration));
        }

        [Test]
        public void ResetRun_AdoptsTheGivenGeneration()
        {
            var balancer = CreateBalancer();

            balancer.ResetRun(7);

            Assert.AreEqual(7, balancer.Generation);
        }

        private static BalloonBalancer CreateBalancer()
        {
            var config = Substitute.For<IGameConfiguration>();
            config.SlotsSize.Returns(new Vector2Int(3, 3));
            var pathHolder = new BalancePathHolder();
            var grid = new SlotGrid(config, pathHolder);
            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            var subscriber = Substitute.For<ISubscriber<BalanceBalloonsMessage>>();

            // DisturbanceField is only dereferenced when animating a non-empty balance, so an
            // empty-grid balancer can omit it.
            return new BalloonBalancer(grid, balloonsConfig, pathHolder, subscriber, null);
        }
    }
}
