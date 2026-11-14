using BalloonParty.Item.Snipe;
using BalloonParty.Projectile.Model;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    /// <summary>
    ///     How the Snipe pencil line cuts a lance's flight into straight runs: from where it armed to the
    ///     first wall, then wall to wall while the lance carries on, and nothing for a pierce no Snipe armed.
    /// </summary>
    [TestFixture]
    public class SnipeLanceTraceTests
    {
        private static readonly Vector3 ArmPoint = new(0f, 1f, 0f);
        private static readonly Vector3 FirstWall = new(0f, 9f, 0f);
        private static readonly Vector3 SecondWall = new(4f, 5f, 0f);

        private SnipeLanceTrace _trace;
        private ProjectileModel _shot;

        [SetUp]
        public void SetUp()
        {
            _trace = new SnipeLanceTrace();
            _shot = new ProjectileModel();
        }

        [Test]
        public void TryCloseRun_FromTheArmPointToTheDischargeWall_DrawsThatRunAndStopsTracking()
        {
            _trace.Open(_shot, ArmPoint);

            Assert.IsTrue(_trace.TryCloseRun(_shot, FirstWall, stillPiercing: false, out var from));
            Assert.AreEqual(ArmPoint, from);
            Assert.IsFalse(_trace.IsOpen(_shot));
        }

        [Test]
        public void TryCloseRun_LanceCarriesOnPastTheWall_NextRunStartsAtThatWall()
        {
            _trace.Open(_shot, ArmPoint);
            _trace.TryCloseRun(_shot, FirstWall, stillPiercing: true, out _);

            Assert.IsTrue(_trace.TryCloseRun(_shot, SecondWall, stillPiercing: false, out var from));
            Assert.AreEqual(FirstWall, from);
        }

        [Test]
        public void TryCloseRun_PierceNoSnipeArmed_DrawsNothing()
        {
            Assert.IsFalse(_trace.TryCloseRun(_shot, FirstWall, stillPiercing: true, out _));
            Assert.IsFalse(_trace.IsOpen(_shot));
        }

        // A charge banked on a running lance re-arms it at the discharge wall: the run already open keeps
        // its own start, so the line still reaches back to where the lance first armed.
        [Test]
        public void Open_WhileARunIsOpen_KeepsTheEarlierStart()
        {
            _trace.Open(_shot, ArmPoint);
            _trace.Open(_shot, FirstWall);

            Assert.IsTrue(_trace.TryCloseRun(_shot, FirstWall, stillPiercing: true, out var from));
            Assert.AreEqual(ArmPoint, from);
        }

        // A charge banked on a cruise pierce opens its run at the very wall that run then closes on: no
        // zero-length stroke, but the lance's next run is traced from there.
        [Test]
        public void TryCloseRun_RunOpenedAtThisSameWall_DrawsNothingButTracesTheNextRun()
        {
            _trace.Open(_shot, FirstWall);

            Assert.IsFalse(_trace.TryCloseRun(_shot, FirstWall, stillPiercing: true, out _));
            Assert.IsTrue(_trace.TryCloseRun(_shot, SecondWall, stillPiercing: false, out var from));
            Assert.AreEqual(FirstWall, from);
        }

        [Test]
        public void Launched_LanceArmedInTheThrower_FirstRunStartsAtTheMuzzle()
        {
            var muzzle = new Vector3(0f, -4f, 0f);
            _trace.OpenAtLaunch(_shot);

            Assert.IsFalse(_trace.IsOpen(_shot), "no run before the shot leaves the thrower");
            _trace.Launched(_shot, muzzle);

            Assert.IsTrue(_trace.TryCloseRun(_shot, FirstWall, stillPiercing: false, out var from));
            Assert.AreEqual(muzzle, from);
        }

        [Test]
        public void Launched_NoLanceArmedInTheThrower_OpensNothing()
        {
            _trace.Launched(_shot, Vector3.zero);

            Assert.IsFalse(_trace.IsOpen(_shot));
        }

        [Test]
        public void Drop_ShotRetiredBeforeItFired_NeverOpensARun()
        {
            _trace.OpenAtLaunch(_shot);
            _trace.Drop(_shot);

            _trace.Launched(_shot, Vector3.zero);

            Assert.IsFalse(_trace.IsOpen(_shot));
        }

        [Test]
        public void Drop_RetiredShot_ForgetsItsRun()
        {
            _trace.Open(_shot, ArmPoint);

            _trace.Drop(_shot);

            Assert.IsFalse(_trace.IsOpen(_shot));
            Assert.AreEqual(0, _trace.OpenCount);
        }

        [Test]
        public void TryCloseRun_TwoShots_TracesEachOnItsOwn()
        {
            var other = new ProjectileModel();
            _trace.Open(_shot, ArmPoint);
            _trace.Open(other, SecondWall);

            Assert.IsTrue(_trace.TryCloseRun(other, FirstWall, stillPiercing: false, out var otherFrom));
            Assert.AreEqual(SecondWall, otherFrom);
            Assert.IsTrue(_trace.IsOpen(_shot));
        }
    }
}
