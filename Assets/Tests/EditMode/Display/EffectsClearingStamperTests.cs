using BalloonParty.Display;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Display
{
    [TestFixture]
    public class EffectsClearingStamperTests
    {
        private const float Tolerance = 1e-5f;

        // A 2:1 portrait view of 10 world units' height, centred on (1, 2).
        private static readonly ClearingView View = new(new Vector2(1f, 2f), 5f, 0.5f);

        private IProjectileRegistry _registry;
        private EffectsClearingStamper _stamper;
        private Vector4[] _stamps;

        [SetUp]
        public void SetUp()
        {
            _registry = Substitute.For<IProjectileRegistry>();
            _stamper = new EffectsClearingStamper();
            _stamps = new Vector4[EffectsClearingStamper.MaxStamps];
        }

        [Test]
        public void ToUv_ViewCentre_IsMiddleOfMask()
        {
            AssertUv(new Vector2(0.5f, 0.5f), View.ToUv(new Vector2(1f, 2f)));
        }

        [Test]
        public void ToUv_TopRightCorner_IsOneOne()
        {
            // Half-height 5, half-width 5 * 0.5 = 2.5.
            AssertUv(Vector2.one, View.ToUv(new Vector2(3.5f, 7f)));
        }

        [Test]
        public void ToUvHeight_ScalesWithOrthographicSize()
        {
            Assert.AreEqual(0.1f, View.ToUvHeight(1f), Tolerance);

            var zoomedIn = new ClearingView(Vector2.zero, 2.5f, 0.5f);
            Assert.AreEqual(0.2f, zoomedIn.ToUvHeight(1f), Tolerance);
        }

        [Test]
        public void Fill_NothingInFlight_StampsNothing()
        {
            InFlight();

            Assert.AreEqual(0, _stamper.Fill(_registry, View, _stamps));
        }

        [Test]
        public void Fill_FirstSighting_StampsADisc()
        {
            var ball = Substitute.For<IProjectileModel>();
            InFlight((ball, new Vector2(1f, 2f)));

            Assert.AreEqual(1, _stamper.Fill(_registry, View, _stamps));
            AssertStamp(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _stamps[0]);
        }

        [Test]
        public void Fill_NextFrame_StampsCapsuleFromLastPosition()
        {
            var ball = Substitute.For<IProjectileModel>();
            InFlight((ball, new Vector2(1f, 2f)));
            _stamper.Fill(_registry, View, _stamps);

            InFlight((ball, new Vector2(1f, 3f)));
            _stamper.Fill(_registry, View, _stamps);

            AssertStamp(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.6f), _stamps[0]);
        }

        [Test]
        public void Fill_AfterAFrameOutOfFlight_StampsADiscAgain()
        {
            var ball = Substitute.For<IProjectileModel>();
            InFlight((ball, new Vector2(1f, 2f)));
            _stamper.Fill(_registry, View, _stamps);
            InFlight();
            _stamper.Fill(_registry, View, _stamps);

            // Back in flight elsewhere: no corridor across the gap.
            InFlight((ball, new Vector2(1f, 3f)));
            _stamper.Fill(_registry, View, _stamps);

            AssertStamp(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), _stamps[0]);
        }

        [Test]
        public void Forget_DropsLastPosition()
        {
            var ball = Substitute.For<IProjectileModel>();
            InFlight((ball, new Vector2(1f, 2f)));
            _stamper.Fill(_registry, View, _stamps);

            _stamper.Forget(ball);
            InFlight((ball, new Vector2(1f, 3f)));
            _stamper.Fill(_registry, View, _stamps);

            AssertStamp(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), _stamps[0]);
        }

        [Test]
        public void Fill_TwoBalls_TrackEachOnItsOwn()
        {
            var first = Substitute.For<IProjectileModel>();
            var second = Substitute.For<IProjectileModel>();
            InFlight((first, new Vector2(1f, 2f)), (second, new Vector2(1f, 4f)));
            _stamper.Fill(_registry, View, _stamps);

            InFlight((first, new Vector2(1f, 3f)), (second, new Vector2(1f, 5f)));

            Assert.AreEqual(2, _stamper.Fill(_registry, View, _stamps));
            AssertStamp(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.6f), _stamps[0]);
            AssertStamp(new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.8f), _stamps[1]);
        }

        [Test]
        public void Fill_MoreBallsThanStamps_KeepsTheMostCentral()
        {
            const int extra = 2;
            var balls = new (IProjectileModel, Vector2)[EffectsClearingStamper.MaxStamps + extra];
            for (var i = 0; i < balls.Length; i++)
            {
                // Each further from the centre than the last, the last two the furthest.
                balls[i] = (Substitute.For<IProjectileModel>(), new Vector2(1f, 2f + 0.25f * i));
            }

            InFlight(balls);

            Assert.AreEqual(EffectsClearingStamper.MaxStamps, _stamper.Fill(_registry, View, _stamps));
            var furthestKept = View.ToUv(balls[EffectsClearingStamper.MaxStamps - 1].Item2).y;
            for (var i = 0; i < EffectsClearingStamper.MaxStamps; i++)
            {
                Assert.LessOrEqual(_stamps[i].w, furthestKept + Tolerance);
            }
        }

        private void InFlight(params (IProjectileModel Model, Vector2 Position)[] balls)
        {
            _registry.InFlightCount.Returns(balls.Length);
            for (var i = 0; i < balls.Length; i++)
            {
                _registry.InFlightAt(i).Returns(balls[i].Model);
                _registry.InFlightPositionAt(i).Returns((Vector3)balls[i].Position);
            }
        }

        private static void AssertUv(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, Tolerance, "y");
        }

        private static void AssertStamp(Vector2 expectedStart, Vector2 expectedEnd, Vector4 stamp)
        {
            AssertUv(expectedStart, new Vector2(stamp.x, stamp.y));
            AssertUv(expectedEnd, new Vector2(stamp.z, stamp.w));
        }
    }
}
