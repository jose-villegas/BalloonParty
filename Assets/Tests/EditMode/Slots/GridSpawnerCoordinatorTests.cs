using System.Collections.Generic;
using System.Threading;
using BalloonParty.Shared.GameState;
using BalloonParty.Slots.Spawner;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Slots
{
    [TestFixture]
    public class GridSpawnerCoordinatorTests
    {
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
                ReadyGate());

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
                ReadyGate());

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
                ReadyGate());

            coordinator.Start();

            Assert.That(called, Is.EquivalentTo(new[] { "a", "b" }),
                "Both spawners at the same stage must run.");
        }

        [Test]
        public void GridSpawnerCoordinator_ResetRun_RerunsSpawners()
        {
            var callCount = 0;

            var spawner = Substitute.For<IGridSpawner>();
            spawner.SpawnPriority.Returns(SpawnStage.StaticActors);
            spawner.SpawnAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                callCount++;
                return UniTask.CompletedTask;
            });

            var coordinator = new GridSpawnerCoordinator(
                new[] { spawner },
                ReadyGate());

            coordinator.Start();
            Assert.AreEqual(1, callCount, "Initial spawn runs once.");

            coordinator.ResetRun(2);

            Assert.AreEqual(2, callCount, "Reset re-runs the spawners to repopulate the board.");
        }

        // A real gate over a substituted INavigation already at Game — resolves synchronously, no static state.
        private static NavigationReadyGate ReadyGate()
        {
            var navigation = Substitute.For<INavigation>();
            navigation.Current.Returns(new ReactiveProperty<NavigationState>(NavigationState.Game));
            return new NavigationReadyGate(navigation, NavigationState.Game);
        }
    }
}

