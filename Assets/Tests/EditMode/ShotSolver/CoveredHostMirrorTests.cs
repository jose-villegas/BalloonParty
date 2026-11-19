using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>
    ///     A cover formation holds the balloon it stands round in its slot. The solver's copy of the board has to
    ///     say so too, or its predicted pulses float the balloon up out of a cover the real board keeps it in. What
    ///     the simulation does with the flag is <see cref="AnchoredTargetFidelityTests" />'s; this is the gather
    ///     reading it off every kind of balloon a cover stands round.
    /// </summary>
    [TestFixture]
    public class CoveredHostMirrorTests
    {
        private static readonly Vector2Int Slot = new(1, 1);

        private SlotGrid _grid;
        private IBalloonsConfiguration _balloons;
        private IGamePalette _palette;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(3, 3));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());
            _balloons = Substitute.For<IBalloonsConfiguration>();
            _palette = Substitute.For<IGamePalette>();
        }

        [TestCase(BalloonType.Simple)]
        [TestCase(BalloonType.Golden)]
        [TestCase(BalloonType.Heart)]
        [TestCase(BalloonType.Disco)]
        [TestCase(BalloonType.Nuke)]
        public void TheGatheredTarget_IsAnchoredWhileACoverHoldsIt_AndNotOnceLetGo(BalloonType type)
        {
            var host = CoverHost(type);
            _grid.Place(host, null, Slot);
            Assert.IsFalse(Gathered().Anchored, "control: a balloon no cover holds is free in the solver's board");

            host.SetHeldByCover(true);
            Assert.IsTrue(Gathered().Anchored);

            host.SetHeldByCover(false);
            Assert.IsFalse(Gathered().Anchored);
        }

        private BalanceProfile Gathered()
        {
            Assert.IsTrue(
                ShotBoardGather.TryBuildTargetSnapshot(_grid, _balloons, _palette, Slot, _grid.At(Slot), out var target),
                "every balloon a cover stands round is a target the solver gathers");
            Assert.IsTrue(target.Balance.HasValue);
            return target.Balance.Value;
        }

        // The plain balloon carries a bomb, as the one a cover stands round does.
        private static BalloonModelBase CoverHost(BalloonType type)
        {
            var config = new BalloonModelConfig(type, scoreValue: 0, hitsToPop: type == BalloonType.Nuke ? 3 : 1);
            switch (type)
            {
                case BalloonType.Golden:
                    return new GoldenBalloonModel(config);
                case BalloonType.Heart:
                    return new HeartBalloonModel(config);
                case BalloonType.Disco:
                    return new DiscoBalloonModel(config);
                case BalloonType.Nuke:
                    return new NukeBalloonModel(config);
                default:
                    var carrier = new BalloonModel(config);
                    carrier.Item.Value = ItemType.Bomb;
                    return carrier;
            }
        }
    }
}
