using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class PierceTelegraphScanTests
    {
        private static readonly IReadOnlyList<PendingPierceHit> NothingPlowed = new List<PendingPierceHit>();

        // The reported bug: a Snipe-armed shot drew no telegraph line because the forward cast stopped at
        // the first balloon it crossed, and that is almost always one the pierce pops in passing.
        [Test]
        public void TrySelectNextTough_OrdinaryBalloonsInFront_StillFindsTheToughBehindThem()
        {
            var unbreakable = new UnbreakableBalloonModel(new BalloonModelConfig());
            var candidates = new List<PierceCastCandidate>
            {
                Candidate(Ordinary(), 0.5f),
                Candidate(Ordinary(), 1.5f),
                Candidate(unbreakable, 3f),
            };

            Assert.IsTrue(PierceTelegraphScan.TrySelectNextTough(candidates, NothingPlowed, out var next));
            Assert.AreSame(unbreakable, next.Balloon);
            Assert.AreEqual(3f, next.Distance);
        }

        [Test]
        public void TrySelectNextTough_SeveralToughs_PicksTheNearestWhateverTheCastOrder()
        {
            var far = Tough();
            var near = Tough();
            var candidates = new List<PierceCastCandidate> { Candidate(far, 4f), Candidate(near, 2f) };

            Assert.IsTrue(PierceTelegraphScan.TrySelectNextTough(candidates, NothingPlowed, out var next));
            Assert.AreSame(near, next.Balloon);
        }

        [Test]
        public void TrySelectNextTough_NearestToughAlreadyPlowed_SkipsToTheNextOne()
        {
            var plowedTough = Tough();
            var nextTough = Tough();
            var candidates = new List<PierceCastCandidate> { Candidate(plowedTough, 1f), Candidate(nextTough, 2f) };
            var plowed = new List<PendingPierceHit> { new(plowedTough, Vector3.zero) };

            Assert.IsTrue(PierceTelegraphScan.TrySelectNextTough(candidates, plowed, out var next));
            Assert.AreSame(nextTough, next.Balloon);
        }

        [Test]
        public void TrySelectNextTough_NoToughOnTheSegment_FindsNothing()
        {
            var plowedTough = Tough();
            var candidates = new List<PierceCastCandidate>
            {
                Candidate(Ordinary(), 1f),
                Candidate(plowedTough, 2f),
                Candidate(null, 3f),
            };
            var plowed = new List<PendingPierceHit> { new(plowedTough, Vector3.zero) };

            Assert.IsFalse(PierceTelegraphScan.TrySelectNextTough(candidates, plowed, out _));
        }

        [Test]
        public void TrySelectNextTough_EmptyCast_FindsNothing()
        {
            Assert.IsFalse(PierceTelegraphScan.TrySelectNextTough(new List<PierceCastCandidate>(), NothingPlowed, out _));
        }

        [Test]
        public void TrySelectNextTough_ReportsTheChosenBalloonsPosition()
        {
            var tough = Tough();
            var position = new Vector3(1f, 2f, 0f);
            var candidates = new List<PierceCastCandidate> { new(tough, 2f, position) };

            Assert.IsTrue(PierceTelegraphScan.TrySelectNextTough(candidates, NothingPlowed, out var next));
            Assert.AreEqual(position, next.Position);
        }

        private static PierceCastCandidate Candidate(IBalloonModel balloon, float distance)
        {
            return new PierceCastCandidate(balloon, distance, Vector3.zero);
        }

        private static IBalloonModel Ordinary()
        {
            return new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
        }

        private static IBalloonModel Tough()
        {
            return new BalloonModel(new BalloonModelConfig(hitsToPop: 3));
        }
    }
}
