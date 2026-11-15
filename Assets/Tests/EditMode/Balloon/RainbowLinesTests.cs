using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Reward;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The clock lines a disco ball's pop owes rainbow (PLAN-DiscoBalloon): each line that brings a balloon pays one,
    ///     its paintable balloons turn rainbow and its immune ones keep their look, the breaking shot can't climb on them,
    ///     and a level change forgets what is still owed.
    /// </summary>
    [TestFixture]
    public class RainbowLinesTests
    {
        private IGamePalette _palette;
        private ProjectileRegistry _registry;
        private ShotPaintLedger _ledger;
        private ReactiveProperty<int> _level;
        private RainbowLines _lines;
        private ProjectileModel _shot;

        [SetUp]
        public void SetUp()
        {
            _palette = Substitute.For<IGamePalette>();
            _palette.IsRainbow(GamePalette.RainbowColorId).Returns(true);
            _registry = new ProjectileRegistry();
            _ledger = new ShotPaintLedger(_registry);
            _level = new ReactiveProperty<int>(1);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Level.Returns(_level);
            _lines = new RainbowLines(_palette, _ledger, _registry, levelProgress);
            _lines.Start();

            _shot = new ProjectileModel();
            _registry.Load(_shot, null);
            _registry.Launch(_shot);
        }

        [TearDown]
        public void TearDown()
        {
            _lines.Dispose();
            _ledger.Dispose();
            _registry.Dispose();
        }

        [Test]
        public void AnOwedLine_PaintsItsPaintableBalloons_AndLeavesTheImmuneOnesTheirLook()
        {
            var earlier = Balloon("Green");
            var red = Balloon("Red");
            var tough = new ToughBalloonModel(new BalloonModelConfig(BalloonType.Tough, hitsToPop: 3));
            _lines.Grant(2, _shot);

            _lines.PaintLine(new IBalloonModel[] { earlier, red, tough }, from: 1);

            Assert.AreEqual("Green", earlier.Color.Value, "a balloon from before the line isn't the line's");
            Assert.AreEqual(GamePalette.RainbowColorId, red.Color.Value);
            Assert.AreEqual(1, _lines.Owed);
        }

        [Test]
        public void WithNoLineOwed_ALineArrivesAsItIs()
        {
            var red = Balloon("Red");

            _lines.PaintLine(new IBalloonModel[] { red }, from: 0);

            Assert.AreEqual("Red", red.Color.Value);
        }

        [Test]
        public void ALineThatBringsNoBalloon_PaysNothing()
        {
            _lines.Grant(1, _shot);

            _lines.PaintLine(new List<IBalloonModel>(), from: 0);

            Assert.AreEqual(1, _lines.Owed);
        }

        [Test]
        public void TheOwedLinesRunOut()
        {
            _lines.Grant(1, _shot);
            _lines.PaintLine(new IBalloonModel[] { Balloon("Red") }, from: 0);
            var late = Balloon("Blue");

            _lines.PaintLine(new IBalloonModel[] { late }, from: 0);

            Assert.AreEqual("Blue", late.Color.Value);
        }

        [Test]
        public void TheBreakingShot_IsKeptOffWhatTheLinesBring_WhileItFlies()
        {
            var red = Balloon("Red");
            _lines.Grant(2, _shot);

            _lines.PaintLine(new IBalloonModel[] { red }, from: 0);

            Assert.IsTrue(_ledger.Painted(_shot, red));
        }

        [Test]
        public void ARetiredShot_BooksNothing_EvenIfItsModelFliesAgain()
        {
            _lines.Grant(2, _shot);
            _registry.Retire(_shot);
            _registry.Load(_shot, null);
            _registry.Launch(_shot);
            var red = Balloon("Red");

            _lines.PaintLine(new IBalloonModel[] { red }, from: 0);

            Assert.AreEqual(GamePalette.RainbowColorId, red.Color.Value);
            Assert.IsFalse(_ledger.Painted(_shot, red));
        }

        [Test]
        public void ASecondPop_TopsTheLinesUp_RatherThanStackingThem()
        {
            _lines.Grant(3, _shot);
            _lines.PaintLine(new IBalloonModel[] { Balloon("Red") }, from: 0);

            _lines.Grant(3, _shot);

            Assert.AreEqual(3, _lines.Owed);
        }

        [Test]
        public void ALevelChange_ForgetsTheLinesOwed()
        {
            _lines.Grant(3, _shot);

            _level.Value = 2;

            Assert.AreEqual(0, _lines.Owed);
        }

        [Test]
        public void ARunReset_ForgetsTheLinesOwed()
        {
            _lines.Grant(3, _shot);

            _lines.ResetRun(1);

            Assert.AreEqual(0, _lines.Owed);
        }

        private static BalloonModel Balloon(string color)
        {
            var model = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            model.Color.Value = color;
            return model;
        }
    }
}
