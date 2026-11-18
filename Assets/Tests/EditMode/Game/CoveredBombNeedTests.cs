using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Items;
using BalloonParty.Game.Assist;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    /// <summary>
    ///     The Snipe roll's need from a bomb under a formation's cover: nothing while the bomb is open or just covered,
    ///     more the longer the cover holds, and from nothing again once the cover opens or the bomb goes.
    /// </summary>
    [TestFixture]
    public class CoveredBombNeedTests
    {
        private static readonly Vector2Int BombSlot = new(2, 2);

        private readonly Vector2Int[] _around = new Vector2Int[6];

        private SlotGrid _grid;
        private IAssistSettings _settings;
        private CoveredBombNeed _need;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(5, 5));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());
            _settings = Substitute.For<IAssistSettings>();
            _settings.CoveredBombNeedWeight.Returns(0.6f);
            _settings.CoveredBombSecondsForFull.Returns(20f);
            _settings.CoveredBombMinCover.Returns(3);
            _now = 0f;
            _need = new CoveredBombNeed(_grid, _settings) { Now = () => _now };
            _need.Start();
            HexCoordinates.HexNeighborIndices(BombSlot.x, BombSlot.y, _around);
        }

        [TearDown]
        public void TearDown()
        {
            _need.Dispose();
        }

        [Test]
        public void Need_ABombJustCovered_IsNone()
        {
            PlaceCarrier(ItemType.Bomb);
            Cover(3);

            Assert.AreEqual(0f, _need.Need, 1e-5f);
        }

        [Test]
        public void Need_GrowsTheLongerTheCoverHolds_UpToTheWeight()
        {
            PlaceCarrier(ItemType.Bomb);
            Cover(3);

            _now = 10f;
            Assert.AreEqual(0.3f, _need.Need, 1e-5f, "half the full time: half the weight");

            _now = 20f;
            Assert.AreEqual(0.6f, _need.Need, 1e-5f, "the full time reads the weight");

            _now = 90f;
            Assert.AreEqual(0.6f, _need.Need, 1e-5f, "never past the weight");
        }

        [Test]
        public void Need_TheCoversTime_StartsWhenItCloses_NotWhenARollFirstAsks()
        {
            PlaceCarrier(ItemType.Bomb);
            _now = 30f;
            Cover(3);

            _now = 40f;

            Assert.AreEqual(0.3f, _need.Need, 1e-5f, "covered for 10 of its 20 seconds, though on the board for 40");
        }

        [Test]
        public void Need_FewerUnbreakablesThanTheCoverTakes_IsNone()
        {
            PlaceCarrier(ItemType.Bomb);
            Cover(2);

            _now = 60f;

            Assert.AreEqual(0f, _need.Need, 1e-5f);
        }

        [Test]
        public void Need_UnbreakablesOutOfFormation_DoNotCover()
        {
            PlaceCarrier(ItemType.Bomb);
            Cover(3, inFormation: false);

            _now = 60f;

            Assert.AreEqual(0f, _need.Need, 1e-5f);
        }

        [Test]
        public void Need_ToughsInFormation_DoNotCover()
        {
            PlaceCarrier(ItemType.Bomb);
            for (var i = 0; i < 3; i++)
            {
                var tough = new ToughBalloonModel(new BalloonModelConfig());
                tough.SetInFormation(true);
                _grid.Place(tough, null, _around[i]);
            }

            _now = 60f;

            Assert.AreEqual(0f, _need.Need, 1e-5f);
        }

        [TestCase(ItemType.None)]
        [TestCase(ItemType.Snipe)]
        [TestCase(ItemType.Lightning)]
        public void Need_ACoveredBalloonWithoutABomb_IsNone(ItemType item)
        {
            PlaceCarrier(item);
            Cover(6);

            _now = 60f;

            Assert.AreEqual(0f, _need.Need, 1e-5f);
        }

        [Test]
        public void Need_ACoverThatOpens_StartsFromNothingWhenItClosesAgain()
        {
            PlaceCarrier(ItemType.Bomb);
            Cover(3);
            _now = 20f;
            Assert.AreEqual(0.6f, _need.Need, 1e-5f);

            var opened = (UnbreakableBalloonModel)_grid.At(_around[0]);
            _grid.Remove(_around[0]);
            Assert.AreEqual(0f, _need.Need, 1e-5f, "two left: open");

            _grid.Place(opened, null, _around[0]);
            _now = 25f;

            Assert.AreEqual(0.15f, _need.Need, 1e-5f, "five seconds into the new cover");
        }

        [Test]
        public void Need_TheBombPopped_IsNone()
        {
            PlaceCarrier(ItemType.Bomb);
            Cover(3);
            _now = 20f;

            _grid.Remove(BombSlot);

            Assert.AreEqual(0f, _need.Need, 1e-5f);
        }

        [Test]
        public void Need_TwoCoveredBombs_ReadsTheLongerCovered()
        {
            PlaceCarrier(ItemType.Bomb);
            Cover(6);
            _now = 10f;
            // A second bomb beside the first's cover: its three neighbours among those unbreakables cover it too.
            var second = new BalloonModel(new BalloonModelConfig());
            second.Item.Value = ItemType.Bomb;
            var beside = new Vector2Int(_around[0].x + (_around[0].x - BombSlot.x), _around[0].y + (_around[0].y - BombSlot.y));
            if (_grid.InBounds(beside) && _grid.IsEmpty(beside.x, beside.y))
            {
                _grid.Place(second, null, beside);
            }

            _now = 20f;

            Assert.AreEqual(0.6f, _need.Need, 1e-5f, "the first has been covered its full time, whatever the second");
        }

        [Test]
        public void Need_ZeroWeight_IsNone()
        {
            _settings.CoveredBombNeedWeight.Returns(0f);
            PlaceCarrier(ItemType.Bomb);
            Cover(6);

            _now = 60f;

            Assert.AreEqual(0f, _need.Need, 1e-5f);
        }

        [Test]
        public void ResetRun_ForgetsEveryCoversTime()
        {
            PlaceCarrier(ItemType.Bomb);
            Cover(3);
            _now = 20f;

            _need.ResetRun(1);

            Assert.AreEqual(0f, _need.Need, 1e-5f, "the same cover counts from the reset");
        }

        private BalloonModel PlaceCarrier(ItemType item)
        {
            var carrier = new BalloonModel(new BalloonModelConfig());
            carrier.Item.Value = item;
            _grid.Place(carrier, null, BombSlot);
            return carrier;
        }

        private void Cover(int count, bool inFormation = true)
        {
            for (var i = 0; i < count; i++)
            {
                var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());
                unbreakable.SetInFormation(inFormation);
                _grid.Place(unbreakable, null, _around[i]);
            }
        }
    }
}
