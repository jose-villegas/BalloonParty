using BalloonParty.Configuration.Effects;
using BalloonParty.Projectile.Clearance;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class ClearanceFieldTests
    {
        private const float Radius = 2f;
        private const float Tolerance = 1e-4f;

        private StubSettings _settings;
        private ClearanceField _field;

        [SetUp]
        public void SetUp()
        {
            _settings = new StubSettings { Radius = Radius, FadeIn = 1f, FadeOut = 1f };
            _field = new ClearanceField(_settings);
        }

        [Test]
        public void Advance_NothingTracked_NoCircles()
        {
            _field.Advance(0.1f);

            Assert.AreEqual(0, _field.Count);
        }

        [Test]
        public void Advance_TrackedBall_EasesInOverFadeInThenHoldsAtFullRadius()
        {
            var ball = new object();

            _field.Track(ball, Vector2.zero);
            _field.Advance(0.5f);
            var half = _field.CircleAt(0);

            _field.Track(ball, Vector2.zero);
            _field.Advance(0.5f);
            var full = _field.CircleAt(0);

            _field.Track(ball, Vector2.zero);
            _field.Advance(0.5f);
            var held = _field.CircleAt(0);

            Assert.AreEqual(0.5f, half.Strength, Tolerance, "smoothstep(0.5) is 0.5");
            Assert.AreEqual(1f, full.Strength, Tolerance);
            Assert.AreEqual(Radius, full.Radius, Tolerance);
            Assert.AreEqual(1f, held.Strength, Tolerance, "strength must clamp at 1");
        }

        [Test]
        public void Advance_QuarterWayIn_StrengthIsSmoothstepped_RadiusScalesWithIt()
        {
            _field.Track(new object(), Vector2.zero);
            _field.Advance(0.25f);

            var circle = _field.CircleAt(0);
            var expected = Mathf.SmoothStep(0f, 1f, 0.25f);

            Assert.AreEqual(expected, circle.Strength, Tolerance);
            Assert.AreEqual(Radius * expected, circle.Radius, Tolerance);
            Assert.Less(circle.Strength, 0.25f, "smoothstep eases in below the linear ramp");
        }

        [Test]
        public void Advance_TrackedBall_CircleFollowsTheBall()
        {
            var ball = new object();

            _field.Track(ball, new Vector2(1f, 2f));
            _field.Advance(0.1f);
            _field.Track(ball, new Vector2(3f, 4f));
            _field.Advance(0.1f);

            Assert.AreEqual(1, _field.Count);
            Assert.AreEqual(new Vector2(3f, 4f), _field.CircleAt(0).Center);
        }

        [Test]
        public void Advance_BallStopsBeingTracked_CircleStaysAtLastPositionAndFadesOverFadeOut()
        {
            FullyIn(new object(), new Vector2(5f, 6f));

            _field.Advance(0.5f);
            var fading = _field.CircleAt(0);

            Assert.AreEqual(1, _field.Count);
            Assert.AreEqual(new Vector2(5f, 6f), fading.Center);
            Assert.AreEqual(0.5f, fading.Strength, Tolerance);
            Assert.AreEqual(Radius * 0.5f, fading.Radius, Tolerance);
        }

        [Test]
        public void Advance_FadeOutCompletes_SlotIsFreed()
        {
            FullyIn(new object(), Vector2.zero);

            _field.Advance(0.5f);
            _field.Advance(0.5f);

            Assert.AreEqual(0, _field.Count);
        }

        [Test]
        public void Advance_OverlongStepAfterUntracked_ClampsAtZeroAndFrees()
        {
            FullyIn(new object(), Vector2.zero);

            _field.Advance(10f);

            Assert.AreEqual(0, _field.Count);
        }

        [Test]
        public void Advance_ZeroFadeIn_CircleIsFullStrengthTheSameFrame()
        {
            _settings.FadeIn = 0f;

            _field.Track(new object(), Vector2.zero);
            _field.Advance(0.016f);

            Assert.AreEqual(1f, _field.CircleAt(0).Strength, Tolerance);
            Assert.AreEqual(Radius, _field.CircleAt(0).Radius, Tolerance);
        }

        [Test]
        public void Advance_ZeroFadeOut_CircleIsGoneTheFrameItsBallStopsBeingTracked()
        {
            _settings.FadeOut = 0f;
            FullyIn(new object(), Vector2.zero);

            _field.Advance(0.016f);

            Assert.AreEqual(0, _field.Count);
        }

        [Test]
        public void Advance_TwoBalls_EachGetsItsOwnCircle()
        {
            _field.Track(new object(), new Vector2(-1f, 0f));
            _field.Track(new object(), new Vector2(1f, 0f));
            _field.Advance(0.5f);

            Assert.AreEqual(2, _field.Count);
            Assert.AreNotEqual(_field.CircleAt(0).Center, _field.CircleAt(1).Center);
        }

        [Test]
        public void Track_ModelKeyReusedAfterItsCircleStoppedBeingTracked_GetsNewSlotWhileOldCircleKeepsFadingInPlace()
        {
            var model = new object();
            FullyIn(model, new Vector2(-4f, 0f));

            // The pooled model lands (untracked for a frame), then is thrown again from elsewhere.
            _field.Advance(0.25f);
            _field.Track(model, new Vector2(4f, 0f));
            _field.Advance(0.25f);

            Assert.AreEqual(2, _field.Count, "old fading circle plus the new flight's circle");
            var old = FindAt(new Vector2(-4f, 0f));
            var fresh = FindAt(new Vector2(4f, 0f));
            Assert.AreEqual(0.5f, old.Strength, Tolerance, "0.25 + 0.25 of fade-out from full strength");
            Assert.AreEqual(Mathf.SmoothStep(0f, 1f, 0.25f), fresh.Strength, Tolerance, "the new flight eases in from zero");
        }

        [Test]
        public void Track_AllSlotsLive_NinthBallIsDropped()
        {
            for (var i = 0; i < ClearanceCircle.MaxCircles; i++)
            {
                _field.Track(new object(), new Vector2(i, 0f));
            }

            _field.Track(new object(), new Vector2(100f, 0f));
            _field.Advance(0.5f);

            Assert.AreEqual(ClearanceCircle.MaxCircles, _field.Count);
            for (var i = 0; i < _field.Count; i++)
            {
                Assert.Less(_field.CircleAt(i).Center.x, 50f, "the ninth ball must not displace a live circle");
            }
        }

        [Test]
        public void Track_AllSlotsTakenAndTwoFading_NinthTakesTheWeakestFadingSlot()
        {
            var balls = new object[ClearanceCircle.MaxCircles];
            for (var i = 0; i < balls.Length; i++)
            {
                balls[i] = new object();
                _field.Track(balls[i], new Vector2(i, 0f));
            }

            _field.Advance(1f);

            // Ball 0 lands one step before ball 1, so ball 0's circle is the weaker of the two fading ones.
            TrackAllExcept(balls, 0, -1);
            _field.Advance(0.25f);
            TrackAllExcept(balls, 0, 1);
            _field.Advance(0.25f);
            Assert.AreEqual(ClearanceCircle.MaxCircles, _field.Count, "every slot is still occupied");

            _field.Track(new object(), new Vector2(100f, 0f));
            TrackAllExcept(balls, 0, 1);
            _field.Advance(0.1f);

            Assert.AreEqual(ClearanceCircle.MaxCircles, _field.Count);
            Assert.IsTrue(HasCircleAtX(100f), "the ninth ball gets a circle");
            Assert.IsFalse(HasCircleAtX(0f), "the weakest fading circle was replaced");
            Assert.IsTrue(HasCircleAtX(1f), "the stronger fading circle survives");
        }

        private void TrackAllExcept(object[] balls, int skipA, int skipB)
        {
            for (var i = 0; i < balls.Length; i++)
            {
                if (i != skipA && i != skipB)
                {
                    _field.Track(balls[i], new Vector2(i, 0f));
                }
            }
        }

        // Tracks the ball for a full FadeIn so its circle is at strength 1; leaves it untracked afterwards.
        private void FullyIn(object key, Vector2 center)
        {
            _field.Track(key, center);
            _field.Advance(_settings.FadeIn > 0f ? _settings.FadeIn : 1f);
        }

        private bool HasCircleAtX(float x)
        {
            for (var i = 0; i < _field.Count; i++)
            {
                if (Mathf.Approximately(_field.CircleAt(i).Center.x, x))
                {
                    return true;
                }
            }

            return false;
        }

        private ClearanceCircle FindAt(Vector2 center)
        {
            for (var i = 0; i < _field.Count; i++)
            {
                if (_field.CircleAt(i).Center == center)
                {
                    return _field.CircleAt(i);
                }
            }

            Assert.Fail("no circle at " + center);
            return default;
        }

        private class StubSettings : IClearanceSettings
        {
            public float Radius { get; set; }
            public float RimSoftness { get; set; }
            public float FadeIn { get; set; }
            public float FadeOut { get; set; }
            public float EffectFloorAlpha { get; set; }
            public float FormationPushSpeed { get; set; }
            public bool PushFormations { get; set; }
            public bool BendTrails { get; set; }
            public bool FadeEffects { get; set; }
        }
    }
}
