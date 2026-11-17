using BalloonParty.Configuration.Effects;
using BalloonParty.Display;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Display
{
    [TestFixture]
    public class EffectsClearingStamperTests
    {
        private const float Tolerance = 1e-5f;
        private const float MinRadius = 0.48f;
        private const float MaxRadius = 1.2f;
        private const int ShieldsForMax = 10;
        private const float LingerSeconds = 0.6f;

        // A 2:1 portrait view of 10 world units' height, centred on (1, 2).
        private static readonly ClearingView View = new(new Vector2(1f, 2f), 5f, 0.5f);

        private IProjectileRegistry _registry;
        private IEffectsClearingSettings _settings;
        private EffectsClearingStamper _stamper;
        private EffectsClearingStamps _stamps;

        [SetUp]
        public void SetUp()
        {
            _registry = Substitute.For<IProjectileRegistry>();
            _settings = Substitute.For<IEffectsClearingSettings>();
            _settings.MinClearRadius.Returns(MinRadius);
            _settings.MaxClearRadius.Returns(MaxRadius);
            _settings.ShieldsForMaxRadius.Returns(ShieldsForMax);
            _settings.Trail.Returns(true);
            _stamper = new EffectsClearingStamper(_settings);
            _settings.LingerSeconds.Returns(LingerSeconds);
            _stamps = new EffectsClearingStamps();
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
        public void ReprojectionFrom_SameView_IsIdentity()
        {
            Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), View.ReprojectionFrom(View));
        }

        // The level-up rig pans and zooms in on the ball: a world point must land where it was in last frame's
        // mask, so a stamp stays on the world, not on the screen.
        [TestCase(1f, 3f, 5f, Description = "pan")]
        [TestCase(1f, 2f, 2.5f, Description = "zoom in")]
        [TestCase(-0.5f, 4f, 3f, Description = "pan and zoom")]
        public void ReprojectionFrom_CameraMoved_KeepsAWorldPointInPlace(float x, float y, float orthographicSize)
        {
            var now = new ClearingView(new Vector2(x, y), orthographicSize, View.Aspect);
            var reprojection = now.ReprojectionFrom(View);

            foreach (var world in new[] { new Vector2(1f, 2f), new Vector2(0.2f, 3.1f), new Vector2(2f, 0.5f) })
            {
                var uv = now.ToUv(world);
                var carried = new Vector2(uv.x * reprojection.x + reprojection.z, uv.y * reprojection.y + reprojection.w);
                AssertUv(View.ToUv(world), carried);
            }
        }

        [Test]
        public void Fill_NothingInFlight_StampsNothing()
        {
            InFlight();

            Assert.AreEqual(0, _stamper.Fill(_registry, View, 0f, _stamps));
        }

        [Test]
        public void Fill_FirstSighting_StampsADisc()
        {
            var ball = Ball();
            InFlight((ball, new Vector2(1f, 2f)));

            Assert.AreEqual(1, _stamper.Fill(_registry, View, 0f, _stamps));
            AssertStamp(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), _stamps.Capsules[0]);
        }

        [Test]
        public void Fill_NextFrame_StampsCapsuleFromLastPosition()
        {
            var ball = Ball();
            InFlight((ball, new Vector2(1f, 2f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            InFlight((ball, new Vector2(1f, 3f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            AssertStamp(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.6f), _stamps.Capsules[0]);
        }

        [Test]
        public void Fill_TrailOff_StampsADiscWhereTheBallIs()
        {
            _settings.Trail.Returns(false);
            var ball = Ball();
            InFlight((ball, new Vector2(1f, 2f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            InFlight((ball, new Vector2(1f, 3f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            // No corridor back to last frame's spot: the mask, wiped each frame, holds only where the ball is.
            AssertStamp(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), _stamps.Capsules[0]);
        }

        [Test]
        public void Fill_AfterAFrameOutOfFlight_StampsADiscAgain()
        {
            var ball = Ball();
            InFlight((ball, new Vector2(1f, 2f)));
            _stamper.Fill(_registry, View, 0f, _stamps);
            InFlight();
            _stamper.Fill(_registry, View, 0f, _stamps);

            // Back in flight elsewhere: no corridor across the gap.
            InFlight((ball, new Vector2(1f, 3f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            AssertStamp(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), _stamps.Capsules[0]);
        }

        [Test]
        public void Forget_DropsLastPosition()
        {
            var ball = Ball();
            InFlight((ball, new Vector2(1f, 2f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            _stamper.Forget(ball);
            InFlight((ball, new Vector2(1f, 3f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            AssertStamp(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), _stamps.Capsules[0]);
        }

        [Test]
        public void Fill_BallInFlight_IsNotFaded()
        {
            InFlight((Ball(), new Vector2(1f, 2f)));

            _stamper.Fill(_registry, View, 0.1f, _stamps);

            Assert.AreEqual(0f, _stamps.Fades[0], Tolerance);
        }

        [Test]
        public void Fill_TrailOff_BallGoneFromFlight_ShrinksAwayWhereItWasLastSeen()
        {
            _settings.Trail.Returns(false);
            InFlight((Ball(), new Vector2(1f, 3f)));
            _stamper.Fill(_registry, View, 0f, _stamps);
            InFlight();

            // Its last glide over, it holds a full dome where it was this frame, so the shrink starts without a gap.
            Assert.AreEqual(1, _stamper.Fill(_registry, View, 0.1f, _stamps));
            AssertStamp(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), _stamps.Capsules[0]);
            Assert.AreEqual(0f, _stamps.Fades[0], Tolerance);

            Assert.AreEqual(1, _stamper.Fill(_registry, View, 0.3f, _stamps));
            Assert.AreEqual(0.5f, _stamps.Fades[0], Tolerance);
            AssertStamp(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), _stamps.Capsules[0]);

            Assert.AreEqual(0, _stamper.Fill(_registry, View, 0.3f, _stamps));
        }

        [Test]
        public void Forget_TrailOff_RetiredBallShrinksAway()
        {
            _settings.Trail.Returns(false);
            var ball = Ball(10);
            InFlight((ball, new Vector2(1f, 3f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            _stamper.Forget(ball);
            InFlight();

            Assert.AreEqual(1, _stamper.Fill(_registry, View, 0.15f, _stamps));
            AssertStamp(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), _stamps.Capsules[0]);
            Assert.AreEqual(0.25f, _stamps.Fades[0], Tolerance);
            // It keeps the radius its shields gave it.
            Assert.AreEqual(View.ToUvHeight(MaxRadius), _stamps.Radii[0], Tolerance);
        }

        [Test]
        public void Fill_TrailOff_OneBallGone_TheOtherStillFlies()
        {
            _settings.Trail.Returns(false);
            var gone = Ball();
            var flying = Ball();
            InFlight((gone, new Vector2(1f, 2f)), (flying, new Vector2(1f, 4f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            InFlight((flying, new Vector2(1f, 5f)));

            Assert.AreEqual(2, _stamper.Fill(_registry, View, 0.1f, _stamps));
        }

        [Test]
        public void Fill_TrailOn_BallGoneFromFlight_LeavesNoStamp()
        {
            var ball = Ball();
            InFlight((ball, new Vector2(1f, 2f)));
            _stamper.Fill(_registry, View, 0f, _stamps);
            _stamper.Forget(ball);
            InFlight();

            // The mask's own decay shrinks the trail; a fading stamp on top would hold it open.
            Assert.AreEqual(0, _stamper.Fill(_registry, View, 0.1f, _stamps));
        }

        [Test]
        public void Fill_TwoBalls_TrackEachOnItsOwn()
        {
            var first = Ball();
            var second = Ball();
            InFlight((first, new Vector2(1f, 2f)), (second, new Vector2(1f, 4f)));
            _stamper.Fill(_registry, View, 0f, _stamps);

            InFlight((first, new Vector2(1f, 3f)), (second, new Vector2(1f, 5f)));

            Assert.AreEqual(2, _stamper.Fill(_registry, View, 0f, _stamps));
            AssertStamp(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.6f), _stamps.Capsules[0]);
            AssertStamp(new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.8f), _stamps.Capsules[1]);
        }

        [Test]
        public void Fill_MoreBallsThanStamps_KeepsTheMostCentral()
        {
            const int extra = 2;
            var balls = new (IProjectileModel, Vector2)[EffectsClearingStamper.MaxStamps + extra];
            for (var i = 0; i < balls.Length; i++)
            {
                // Each further from the centre than the last, the last two the furthest.
                balls[i] = (Ball(), new Vector2(1f, 2f + 0.25f * i));
            }

            InFlight(balls);

            Assert.AreEqual(EffectsClearingStamper.MaxStamps, _stamper.Fill(_registry, View, 0f, _stamps));
            var furthestKept = View.ToUv(balls[EffectsClearingStamper.MaxStamps - 1].Item2).y;
            for (var i = 0; i < EffectsClearingStamper.MaxStamps; i++)
            {
                Assert.LessOrEqual(_stamps.Capsules[i].w, furthestKept + Tolerance);
            }
        }

        // A view 10 units high, so a radius in view heights is a tenth of its world radius. Evenly from 0.48 at
        // one shield to 1.2 at ten.
        [TestCase(1, 0.048f)]
        [TestCase(4, 0.072f)]
        [TestCase(10, 0.12f)]
        public void Fill_RadiusGrowsEvenlyWithShields(int shields, float expected)
        {
            InFlight((Ball(shields), new Vector2(1f, 2f)));

            _stamper.Fill(_registry, View, 0f, _stamps);

            Assert.AreEqual(expected, _stamps.Radii[0], Tolerance);
        }

        [Test]
        public void Fill_NoShieldsLeft_ClearsAsOneShield()
        {
            InFlight((Ball(0), new Vector2(1f, 2f)));

            _stamper.Fill(_registry, View, 0f, _stamps);

            Assert.AreEqual(View.ToUvHeight(MinRadius), _stamps.Radii[0], Tolerance);
        }

        [Test]
        public void Fill_MoreShieldsThanForMax_CapsAtMaxRadius()
        {
            InFlight((Ball(25), new Vector2(1f, 2f)));

            _stamper.Fill(_registry, View, 0f, _stamps);

            Assert.AreEqual(View.ToUvHeight(MaxRadius), _stamps.Radii[0], Tolerance);
        }

        [Test]
        public void Fill_TwoBalls_EachGetsItsOwnRadius()
        {
            InFlight((Ball(1), new Vector2(1f, 2f)), (Ball(2), new Vector2(1f, 4f)));

            _stamper.Fill(_registry, View, 0f, _stamps);

            Assert.AreEqual(View.ToUvHeight(MinRadius), _stamps.Radii[0], Tolerance);
            Assert.AreEqual(View.ToUvHeight(MinRadius + (MaxRadius - MinRadius) / (ShieldsForMax - 1)), _stamps.Radii[1],
                Tolerance);
        }

        private static IProjectileModel Ball(int shields = 1)
        {
            var ball = Substitute.For<IProjectileModel>();
            ball.ShieldsRemaining.Returns(new ReactiveProperty<int>(shields));
            return ball;
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
