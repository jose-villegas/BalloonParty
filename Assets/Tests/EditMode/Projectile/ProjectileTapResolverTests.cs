using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     The tap economy's own rules, exercised directly. Everything here used to require either a
    ///     scene-constructed <c>ProjectileView</c> reached by reflection (the sweep rule) or a full motion
    ///     step (the mint guard); with the rules owned by one plain-C# type and the corridor probe arriving
    ///     as a delegate, a fixture needs neither a <c>GameObject</c> nor Physics2D.
    /// </summary>
    [TestFixture]
    public class ProjectileTapResolverTests
    {
        private const int SweepRunLength = 2;

        private ProjectileTapResolver _taps;
        private ProjectileModel _projectile;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(new Vector4(5f, 5f, -5f, -5f));
            config.SweepEnabled.Returns(true);
            config.SweepTapThreshold.Returns(SweepRunLength);
            config.CruiseTapCurve.Returns(AnimationCurve.Linear(0f, 0f, 1f, 1f));

            _taps = new ProjectileTapResolver(config);
            _projectile = new ProjectileModel { Speed = 1f };

            // A segment that cleanly cleared: one pop, no tougher contact, and a wall hit to claim.
            _projectile.Flight.SegmentPopCount = 1;
            _projectile.Flight.SegmentSweepValid = true;
            _projectile.Flight.LastBouncePosition = Vector3.zero;
            _projectile.Flight.WallHitSequence = 1;
        }

        [Test]
        public void TryGrantTap_TwiceOnTheSameWallHit_MintsOnlyOnce()
        {
            Assert.IsTrue(_taps.TryGrantTap(_projectile), "the first rule to claim this wall gets the tap");
            Assert.IsFalse(_taps.TryGrantTap(_projectile), "the second is refused");
            Assert.AreEqual(1, _projectile.Flight.TotalCruiseTaps);
        }

        [Test]
        public void TryGrantTap_OnTheNextWallHit_MintsAgain()
        {
            _taps.TryGrantTap(_projectile);
            _projectile.Flight.WallHitSequence++;

            Assert.IsTrue(_taps.TryGrantTap(_projectile), "the guard is per wall hit, not a latch");
            Assert.AreEqual(2, _projectile.Flight.TotalCruiseTaps);
        }

        [Test]
        public void TryAwardSweepTap_CorridorStillBlocked_BreaksTheRun()
        {
            _projectile.Flight.ConsecutiveSweeps = 1;

            var outcome = _taps.TryAwardSweepTap(_projectile, Vector3.right, Vector3.right, Blocked(true));

            Assert.AreEqual(ProjectileSweepOutcome.RunBroken, outcome);
            Assert.AreEqual(0, _projectile.Flight.ConsecutiveSweeps, "a blocked corridor restarts the count");
            Assert.AreEqual(0, _projectile.Flight.TotalCruiseTaps);
        }

        [Test]
        public void TryAwardSweepTap_CleanButShortOfTheRunLength_CreditsWithoutPaying()
        {
            var outcome = _taps.TryAwardSweepTap(_projectile, Vector3.right, Vector3.right, Blocked(false));

            Assert.AreEqual(ProjectileSweepOutcome.Credited, outcome);
            Assert.AreEqual(1, _projectile.Flight.ConsecutiveSweeps, "the pass counts toward the run");
            Assert.AreEqual(0, _projectile.Flight.TotalCruiseTaps, "but one pass is short of a run of two");
        }

        [Test]
        public void TryAwardSweepTap_CleanPassCompletingTheRun_PaysATap()
        {
            _projectile.Flight.ConsecutiveSweeps = SweepRunLength - 1;

            var outcome = _taps.TryAwardSweepTap(_projectile, Vector3.right, Vector3.right, Blocked(false));

            Assert.AreEqual(ProjectileSweepOutcome.Paid, outcome);
            Assert.AreEqual(1, _projectile.Flight.TotalCruiseTaps, "the completed run mints its tap");
        }

        [Test]
        public void TryAwardSweepTap_SegmentWithNoPops_BreaksTheRunWithoutProbing()
        {
            // An empty traversal is cruise's business, not a clearing pass — and the rule shouldn't even
            // need to ask about the corridor to know that.
            _projectile.Flight.SegmentPopCount = 0;
            _projectile.Flight.ConsecutiveSweeps = 1;
            var probed = false;

            var outcome = _taps.TryAwardSweepTap(
                _projectile, Vector3.right, Vector3.right,
                (_, _, _) =>
                {
                    probed = true;
                    return false;
                });

            Assert.AreEqual(ProjectileSweepOutcome.RunBroken, outcome);
            Assert.AreEqual(0, _projectile.Flight.ConsecutiveSweeps);
            Assert.IsFalse(probed, "the cheap gates come first — no corridor probe on a segment with no pops");
        }

        private static PathTrace.SegmentBlocked Blocked(bool blocked)
        {
            return (_, _, _) => blocked;
        }
    }
}
