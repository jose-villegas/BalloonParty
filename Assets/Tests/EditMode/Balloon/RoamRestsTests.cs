using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    // A lone unbreakable stands a number of cycles before it moves again, and only one is on its way at a time.
    [TestFixture]
    public class RoamRestsTests
    {
        private readonly RoamRests _rests = new();

        [SetUp]
        public void SetUp()
        {
            _rests.Clear();
        }

        [Test]
        public void ARoamer_CountsTheCyclesItStands_FromNoneWhenItArrives()
        {
            var roamer = new BalloonModel();

            Assert.AreEqual(0, Cycle(roamer), "new to the board");
            Assert.AreEqual(1, Cycle(roamer));
            Assert.AreEqual(2, Cycle(roamer));
        }

        [Test]
        public void AMove_StartsItsRestOver()
        {
            var roamer = new BalloonModel();
            Cycle(roamer);
            Cycle(roamer);

            _rests.Moved(roamer);

            Assert.AreEqual(0, _rests.CyclesAtRest(roamer));
            Assert.AreEqual(1, Cycle(roamer));
        }

        [Test]
        public void ARoamerNotSeenForACycle_StartsOverWhenItIsBack()
        {
            // Off the board, or in a formation for a while: it rests again once it's alone.
            var roamer = new BalloonModel();
            Cycle(roamer);
            Cycle(roamer);

            _rests.BeginCycle();
            _rests.EndCycle();

            Assert.AreEqual(0, Cycle(roamer));
        }

        [Test]
        public void TheLastMover_HoldsTheOthers_UntilItHasLanded()
        {
            var roamer = new BalloonModel();
            Cycle(roamer);
            _rests.Moved(roamer);
            roamer.IsStable.Value = false;

            Assert.IsTrue(_rests.LastMoverOnItsWay);

            roamer.IsStable.Value = true;

            Assert.IsFalse(_rests.LastMoverOnItsWay);
        }

        [Test]
        public void TheLastMover_GoneFromTheBoard_HoldsNobody()
        {
            var roamer = new BalloonModel();
            Cycle(roamer);
            _rests.Moved(roamer);
            roamer.IsStable.Value = false;

            _rests.BeginCycle();
            _rests.EndCycle();

            Assert.IsFalse(_rests.LastMoverOnItsWay);
        }

        private int Cycle(BalloonModel roamer)
        {
            _rests.BeginCycle();
            var cycles = _rests.Stand(roamer);
            _rests.EndCycle();
            return cycles;
        }
    }
}
