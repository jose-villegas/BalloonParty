using BalloonParty.Balloon.Model;
using BalloonParty.Game.Assist;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    /// <summary>
    ///     The Snipe roll's need from armour left standing: more the more toughs and unbreakables there are and the
    ///     longer they have stood, nothing from ordinary balloons, and forgotten once they leave the board.
    /// </summary>
    [TestFixture]
    public class ArmourNeedTests
    {
        private SlotGrid _grid;
        private IAssistSettings _settings;
        private ArmourNeed _armour;
        private float _now;

        [SetUp]
        public void SetUp()
        {
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(3, 3));
            _grid = new SlotGrid(gridConfig, new BalancePathHolder());
            _settings = Substitute.For<IAssistSettings>();
            _settings.ArmourNeedWeight.Returns(0.5f);
            _settings.ArmourCountForFull.Returns(2f);
            _settings.ArmourSecondsForFull.Returns(10f);
            _now = 0f;
            _armour = new ArmourNeed(_grid, _settings) { Now = () => _now };
            _armour.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _armour.Dispose();
        }

        [Test]
        public void Need_ArmourJustLanded_IsNone()
        {
            Place(new ToughBalloonModel(new BalloonModelConfig()), 0, 0);
            Place(new UnbreakableBalloonModel(new BalloonModelConfig()), 1, 0);

            Assert.AreEqual(0f, _armour.Need, 1e-5f);
        }

        [Test]
        public void Need_GrowsWithHowLongTheArmourHasStood_UpToTheWeight()
        {
            Place(new ToughBalloonModel(new BalloonModelConfig()), 0, 0);
            Place(new UnbreakableBalloonModel(new BalloonModelConfig()), 1, 0);

            _now = 5f;
            Assert.AreEqual(0.25f, _armour.Need, 1e-5f, "both half their full age: half the armour");

            _now = 10f;
            Assert.AreEqual(0.5f, _armour.Need, 1e-5f, "full armour reads the weight");

            _now = 60f;
            Assert.AreEqual(0.5f, _armour.Need, 1e-5f, "never past the weight");
        }

        [Test]
        public void Need_GrowsWithHowMuchArmourStands()
        {
            Place(new ToughBalloonModel(new BalloonModelConfig()), 0, 0);
            _now = 10f;

            Assert.AreEqual(0.25f, _armour.Need, 1e-5f, "one of the two that make full armour");
        }

        [Test]
        public void Need_OrdinaryBalloons_CountForNothing()
        {
            Place(new BalloonModel(new BalloonModelConfig()), 0, 0);
            Place(new BalloonModel(new BalloonModelConfig()), 1, 0);
            _now = 30f;

            Assert.AreEqual(0f, _armour.Need, 1e-5f);
        }

        [Test]
        public void Need_ArmourGone_IsForgotten_AndOneLandingLaterStartsFromNothing()
        {
            Place(new ToughBalloonModel(new BalloonModelConfig()), 0, 0);
            _now = 10f;
            Assert.Greater(_armour.Need, 0f);

            _grid.Remove(new Vector2Int(0, 0));
            Assert.AreEqual(0f, _armour.Need, 1e-5f, "popped armour stands no longer");

            Place(new ToughBalloonModel(new BalloonModelConfig()), 0, 0);
            Assert.AreEqual(0f, _armour.Need, 1e-5f, "a new tough counts from its own landing");
        }

        [Test]
        public void Need_AMovedBalloon_KeepsItsAge()
        {
            var tough = new ToughBalloonModel(new BalloonModelConfig());
            Place(tough, 0, 0);
            _now = 10f;

            _grid.Remove(new Vector2Int(0, 0));
            Place(tough, 2, 2);

            Assert.AreEqual(0.25f, _armour.Need, 1e-5f, "the same armour, still standing");
        }

        [Test]
        public void Need_RunReset_StartsTheAgesOver()
        {
            Place(new ToughBalloonModel(new BalloonModelConfig()), 0, 0);
            _now = 10f;

            _armour.ResetRun(1);

            Assert.AreEqual(0f, _armour.Need, 1e-5f);
        }

        [Test]
        public void Need_NoWeight_IsNone()
        {
            _settings.ArmourNeedWeight.Returns(0f);
            Place(new ToughBalloonModel(new BalloonModelConfig()), 0, 0);
            _now = 10f;

            Assert.AreEqual(0f, _armour.Need, 1e-5f);
        }

        private void Place(BalloonModelBase balloon, int col, int row)
        {
            _grid.Place(balloon, null, new Vector2Int(col, row));
        }
    }
}
