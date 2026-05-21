using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared;
using BalloonParty.Slots;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class GridSpawnerCoordinatorTests
    {
        // Resolves immediately — removes the static Navigation dependency from tests.
        private sealed class ImmediateGate : IReadyGate
        {
            public UniTask WaitAsync(CancellationToken ct) => UniTask.CompletedTask;
        }

        [Test]
        public void GridSpawnerCoordinator_CallsSpawnersInPriorityOrder()
        {
            var callOrder = new List<int>();

            var highPriority = Substitute.For<IGridSpawner>();
            highPriority.SpawnPriority.Returns(SpawnStage.BalloonActors);
            highPriority.SpawnAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                callOrder.Add((int)SpawnStage.BalloonActors);
                return UniTask.CompletedTask;
            });

            var lowPriority = Substitute.For<IGridSpawner>();
            lowPriority.SpawnPriority.Returns(SpawnStage.StaticActors);
            lowPriority.SpawnAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                callOrder.Add((int)SpawnStage.StaticActors);
                return UniTask.CompletedTask;
            });

            // Intentionally pass high-priority first to verify sorting.
            var coordinator = new GridSpawnerCoordinator(
                new[] { highPriority, lowPriority },
                new ImmediateGate());

            coordinator.Start();

            Assert.AreEqual(
                new[] { (int)SpawnStage.StaticActors, (int)SpawnStage.BalloonActors },
                callOrder.ToArray());
        }

        [Test]
        public void GridSpawnerCoordinator_AwaitsEachStageInSequence()
        {
            var executionLog = new List<string>();

            var first = Substitute.For<IGridSpawner>();
            first.SpawnPriority.Returns(SpawnStage.StaticActors);
            first.SpawnAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                executionLog.Add("first_start");
                executionLog.Add("first_end");
                return UniTask.CompletedTask;
            });

            var second = Substitute.For<IGridSpawner>();
            second.SpawnPriority.Returns(SpawnStage.BalloonActors);
            second.SpawnAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                executionLog.Add("second_start");
                return UniTask.CompletedTask;
            });

            var coordinator = new GridSpawnerCoordinator(
                new[] { first, second },
                new ImmediateGate());

            coordinator.Start();

            var firstEndIdx = executionLog.IndexOf("first_end");
            var secondStartIdx = executionLog.IndexOf("second_start");

            Assert.Less(firstEndIdx, secondStartIdx,
                "Second stage must not start until the first stage has completed.");
        }

        [Test]
        public void GridSpawnerCoordinator_SpawnersAtSamePriority_AllRun()
        {
            var called = new List<string>();

            var a = Substitute.For<IGridSpawner>();
            a.SpawnPriority.Returns(SpawnStage.DynamicActors);
            a.SpawnAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                called.Add("a");
                return UniTask.CompletedTask;
            });

            var b = Substitute.For<IGridSpawner>();
            b.SpawnPriority.Returns(SpawnStage.DynamicActors);
            b.SpawnAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                called.Add("b");
                return UniTask.CompletedTask;
            });

            var coordinator = new GridSpawnerCoordinator(
                new[] { a, b },
                new ImmediateGate());

            coordinator.Start();

            Assert.That(called, Is.EquivalentTo(new[] { "a", "b" }),
                "Both spawners at the same stage must run.");
        }
    }
}

