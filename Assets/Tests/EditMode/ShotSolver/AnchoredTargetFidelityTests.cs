using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>A formation member holds its slot live (@ref plan_unbreakable_formations); the sim has to
    /// agree, or the solver aims at a balloon the real board would never move.</summary>
    [TestFixture]
    public class AnchoredTargetFidelityTests
    {
        [Test]
        public void Simulate_UnanchoredTargetWithBias_IsPulsedUpOntoTheShotsLine()
        {
            var result = RunFlight(anchored: false);

            Assert.AreEqual(1, result.Pops, "control: the same board pulses the target onto the line");
        }

        [Test]
        public void Simulate_AnchoredTargetWithTheSameBias_StaysPutAndIsMissed()
        {
            var result = RunFlight(anchored: true);

            Assert.AreEqual(0, result.Pops, "an anchored target ignores the pulse, so the shot passes beside it");
            Assert.IsFalse(result.BoardCleared);
        }

        [Test]
        public void Dynamics_TargetActor_CarriesTheSnapshotsAnchoredFlag()
        {
            var anchored = BuildDynamics(anchored: true);
            var free = BuildDynamics(anchored: false);

            Assert.IsTrue(anchored.TargetActors[0].IsAnchored);
            Assert.IsFalse(free.TargetActors[0].IsAnchored);
        }

        private static ShotBoardDynamics BuildDynamics(bool anchored, ShotBalloonSnapshot[] board = null)
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(3, 3));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            gridConfig.SlotsOffset.Returns(Vector2.zero);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(0.01f);

            board ??= new[] { Target(anchored) };
            var otherDynamicActors = new[]
            {
                new ShotDynamicActorSnapshot(
                    new Vector2Int(0, 2), balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 0f,
                    directBalanceMotion: false, omnidirectional: false, colorId: "Red",
                    biasKind: BalanceBiasKind.None, biasValue: 0f, biasTypeId: 0),
            };
            var staticActors = new[] { new ShotStaticActorSnapshot(new Vector2Int(0, 1)) };
            return new ShotBoardDynamics(gridConfig, balloonsConfig, board, otherDynamicActors, staticActors);
        }

        // Same rig as BalanceBiasFidelityTests.Simulate_BalanceBiasFlipsWhichHopTheBalancerTakes: the bias
        // sends the mover at (1,1) straight up to (1,0), where the vertical shot is.
        private static ShotSimulationResult RunFlight(bool anchored)
        {
            var board = new[] { Target(anchored) };
            var dynamics = BuildDynamics(anchored, board);
            var workingSet = new ShotBalloonState[board.Length];

            return ShotSimulator.Simulate(
                board, new Vector4(1000f, 1000f, -1000f, -1000f), new Vector2(1.5f, -10f), Vector2.up,
                startingShields: 1, projectileContactRadius: 0f, workingSet: workingSet, projectileSpeed: 1f,
                dynamics: dynamics);
        }

        private static ShotBalloonSnapshot Target(bool anchored)
        {
            var balance = new BalanceProfile(
                new Vector2Int(1, 1), 0, 0, 100f, false, null, false, BalanceBiasKind.ColorDiagonal, 5f, 0,
                anchored);
            return ShotBalloonSnapshot.ForColorTarget(new Vector2(2.5f, -1f), 0.2f, "Red", 1, 1, balance);
        }
    }
}
