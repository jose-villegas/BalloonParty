using System.Collections.Generic;
using BalloonParty.Configuration.CardReveal;
using BalloonParty.UI.CardReveal;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class CardRevealViewTests
    {
        private const float Tolerance = 1e-5f;
        private const float AscentSeconds = 4f;

        private ICardRevealSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<ICardRevealSettings>();
            _settings.RibbonsGoneAtAscent.Returns(0.7f);
            _settings.RibbonSecondsWithoutAscent.Returns(2.6f);
            _settings.RibbonMinimumSeconds.Returns(1.8f);
        }

        [Test]
        public void CappedDissolve_WaitsForTheCappedRibbonToLandAndStand()
        {
            _settings.RibbonBurstSeconds.Returns(0.4f);
            _settings.CappedDissolveDelay.Returns(0.5f);
            _settings.CappedDissolveSeconds.Returns(1f);

            Assert.AreEqual(0f, CardRevealView.CappedDissolve(0.9f, _settings), Tolerance);
            Assert.AreEqual(0.5f, CardRevealView.CappedDissolve(1.4f, _settings), Tolerance);
            Assert.AreEqual(1f, CardRevealView.CappedDissolve(5f, _settings), Tolerance);
        }

        [Test]
        public void CappedDissolve_WithNoTimeToDissolve_GoesAtOnce()
        {
            _settings.RibbonBurstSeconds.Returns(0.4f);
            _settings.CappedDissolveDelay.Returns(0.5f);
            _settings.CappedDissolveSeconds.Returns(0f);

            Assert.AreEqual(0f, CardRevealView.CappedDissolve(0.8f, _settings));
            Assert.AreEqual(1f, CardRevealView.CappedDissolve(0.9f, _settings));
        }

        [Test]
        public void DissolvedAt_IsWholeBeforeAndGoneAfter_Everywhere()
        {
            for (var x = -60f; x <= 60f; x += 3f)
            {
                Assert.AreEqual(0f, RibbonGraphic.DissolvedAt(x, 60f, 0f));
                Assert.AreEqual(1f, RibbonGraphic.DissolvedAt(x, 60f, 1f));
            }
        }

        [Test]
        public void DissolvedAt_NeverGrowsBackAsTheDissolveRuns()
        {
            for (var x = -60f; x <= 60f; x += 3f)
            {
                var last = 0f;
                for (var progress = 0f; progress <= 1f; progress += 0.05f)
                {
                    var crumbled = RibbonGraphic.DissolvedAt(x, 60f, progress);
                    Assert.GreaterOrEqual(crumbled, last, $"x {x}, progress {progress}");
                    last = crumbled;
                }
            }
        }

        [Test]
        public void DissolvedAt_HalfwayThrough_HasEatenTheLeftMoreThanTheRight()
        {
            float Share(float from, float to)
            {
                var total = 0f;
                var count = 0;
                for (var x = from; x <= to; x += 1f)
                {
                    total += RibbonGraphic.DissolvedAt(x, 60f, 0.5f);
                    count++;
                }

                return total / count;
            }

            Assert.Greater(Share(-60f, -20f), Share(20f, 60f));
        }

        [Test]
        public void RibbonsGoneAt_WithAnAscentSinceTheRevealStarted_IsItsShareOfTheAscent()
        {
            // Reveal at 10, ascent from 10.2, ribbons out at 10.7: gone 0.7 of the way through a 4 s ascent.
            Assert.AreEqual(10.2f + 2.8f, CardRevealView.RibbonsGoneAt(10.7f, 10f, 10.2f, AscentSeconds, _settings),
                Tolerance);
        }

        [Test]
        public void RibbonsGoneAt_AnAscentEndingTooSoon_StillGivesTheShortestFlight()
        {
            // The ascent began well before the ribbons were out: they still fly long enough to be read.
            Assert.AreEqual(13f + 1.8f, CardRevealView.RibbonsGoneAt(13f, 10f, 10.1f, AscentSeconds, _settings),
                Tolerance);
        }

        [Test]
        public void RibbonsGoneAt_WithNoAscent_IsTheirFlightWithoutOne()
        {
            Assert.AreEqual(10.7f + 2.6f,
                CardRevealView.RibbonsGoneAt(10.7f, 10f, float.NegativeInfinity, AscentSeconds, _settings), Tolerance);
        }

        [Test]
        public void RibbonsGoneAt_AnAscentFromBeforeTheReveal_IsNotThisOnes()
        {
            // A reveal that waited its turn: the ascent that ran before it doesn't time its ribbons.
            Assert.AreEqual(10.7f + 2.6f, CardRevealView.RibbonsGoneAt(10.7f, 10f, 6f, AscentSeconds, _settings),
                Tolerance);
        }

        [Test]
        public void RibbonsGoneAt_WithTimeAfterTheAscentsShare_StaysThatMuchLonger()
        {
            // As the first case, a second more after the ascent's share.
            _settings.RibbonsAfterAscentSeconds.Returns(1f);
            Assert.AreEqual(10.2f + 2.8f + 1f,
                CardRevealView.RibbonsGoneAt(10.7f, 10f, 10.2f, AscentSeconds, _settings), Tolerance);
        }

        [Test]
        public void PinPose_LongBeforeContact_IsOutOfSight()
        {
            SetPin();
            Assert.AreEqual(0f, CardRevealView.PinPose(-5f, _settings).y, Tolerance);
        }

        [Test]
        public void PinLeadSeconds_IsItsWayInWindupAndJab()
        {
            SetPin();
            Assert.AreEqual(0.3f + 0.2f + 0.1f, CardRevealView.PinLeadSeconds(_settings), Tolerance);
        }

        [Test]
        public void PinPose_AsItWindsUp_HoversFullyShownByThePuncture()
        {
            // Wind-up starts the jab's and wind-up's 0.3 s before contact.
            SetPin();
            var hovering = CardRevealView.PinPose(-0.3f, _settings);
            Assert.AreEqual(12f, hovering.x, 1e-3f);
            Assert.AreEqual(1f, hovering.y, Tolerance);
        }

        [Test]
        public void PinPose_AsItJabs_IsDrawnBackFurther()
        {
            SetPin();
            Assert.AreEqual(12f + 10f, CardRevealView.PinPose(-0.1f, _settings).x, 1e-3f);
        }

        [Test]
        public void PinPose_TheJab_SpeedsUpToTheRubber()
        {
            // Accelerating: it covers less of the way in the jab's first half than in its second.
            SetPin();
            var start = CardRevealView.PinPose(-0.1f, _settings).x;
            var middle = CardRevealView.PinPose(-0.05f, _settings).x;
            var end = CardRevealView.PinPose(0f, _settings).x;
            Assert.Less(start - middle, middle - end);
        }

        [Test]
        public void PinPose_AtContactAndWhileItStays_IsInTheRubber()
        {
            SetPin();
            Assert.AreEqual(-3f, CardRevealView.PinPose(0f, _settings).x, 1e-3f);
            Assert.AreEqual(-3f, CardRevealView.PinPose(0.05f, _settings).x, 1e-3f);
        }

        [Test]
        public void PinPose_OnceGone_IsThrownBackAndFaded()
        {
            SetPin();
            var gone = CardRevealView.PinPose(0.08f + 0.4f, _settings);
            Assert.AreEqual(36f, gone.x, 1e-3f);
            Assert.AreEqual(0f, gone.y, Tolerance);
        }

        [Test]
        public void PinPose_WithAPhaseOfNoLength_SkipsIt()
        {
            // No jab time: drawn back until contact, then in the rubber at once.
            SetPin();
            _settings.PinJabSeconds.Returns(0f);
            Assert.AreEqual(12f + 10f, CardRevealView.PinPose(-0.001f, _settings).x, 1e-2f);
            Assert.AreEqual(-3f, CardRevealView.PinPose(0f, _settings).x, 1e-3f);
        }

        [Test]
        public void ContactAt_WithAQuickPin_IsWhenTheBalloonRests()
        {
            SetPin();
            _settings.RiseSeconds.Returns(0.8f);
            _settings.HoldBeforePopSeconds.Returns(0.5f);
            Assert.AreEqual(1.3f, CardRevealView.ContactAt(true, _settings), Tolerance);
        }

        [Test]
        public void ContactAt_WithASlowPin_WaitsForItToGoIn()
        {
            SetPin();
            _settings.RiseSeconds.Returns(0.2f);
            _settings.HoldBeforePopSeconds.Returns(0.1f);
            Assert.AreEqual(0.6f, CardRevealView.ContactAt(true, _settings), Tolerance);
            Assert.AreEqual(0.3f, CardRevealView.ContactAt(false, _settings), Tolerance);
        }

        [Test]
        public void Puncture_OfAListedPop_IsItsOwn()
        {
            var punctures = new List<Vector2> { new(0.2f, 0.3f), new(0.6f, 0.7f) };
            Assert.AreEqual(new Vector2(0.6f, 0.7f), CardRevealView.Puncture(1, punctures));
        }

        [Test]
        public void Puncture_OfAPopNotListed_IsTheTopOfTheBalloonsFront()
        {
            var punctures = new List<Vector2> { new(0.2f, 0.3f) };
            Assert.AreEqual(new Vector2(0.5f, 0.8f), CardRevealView.Puncture(3, punctures));
        }

        [Test]
        public void FloatOffset_AtTheStart_IsTheRiseDistanceBelow()
        {
            SetFloat();
            Assert.AreEqual(-200f, CardRevealView.FloatOffset(0f, 200f, _settings).y, 1e-3f);
        }

        [Test]
        public void FloatOffset_AtRiseSeconds_FloatsTheOvershootPastItsPlace()
        {
            SetFloat();
            Assert.AreEqual(0.1f * 200f, CardRevealView.FloatOffset(0.8f, 200f, _settings).y, 1e-2f);
        }

        [Test]
        public void FloatOffset_LongAfter_HasBobbedAndSwayedToRest()
        {
            SetFloat();
            var settled = CardRevealView.FloatOffset(10f, 200f, _settings);
            Assert.AreEqual(0f, settled.x, 1e-2f);
            Assert.AreEqual(0f, settled.y, 1e-2f);
            Assert.AreEqual(0f, settled.z, 1e-2f);
        }

        [Test]
        public void FloatOffset_SwayingRight_LeansRight()
        {
            // A quarter into the first sway it is moving right: it leans clockwise, a negative z.
            SetFloat();
            Assert.Less(CardRevealView.FloatOffset(0.05f, 200f, _settings).z, 0f);
        }

        [Test]
        public void FallSwing_BeforeTheRubberTears_IsNothing()
        {
            UseSwing();
            Assert.AreEqual(Vector2.zero, CardRevealView.FallSwing(0.09f, 1f, 1f, 1f, _settings));
        }

        [Test]
        public void FallSwing_StartsSquare_WhereverItsPhase()
        {
            // Torn at frame 3 of 30 a second: at the tear the swing hasn't grown in yet.
            UseSwing();
            Assert.AreEqual(0f, CardRevealView.FallSwing(0.1f, 1f, 1f, 1.3f, _settings).magnitude, Tolerance);
        }

        [Test]
        public void FallSwing_OnceGrown_SwingsSideToSideWithinItsReach()
        {
            // A 0.4 s swing, grown in by 0.5 s after the tear: half a swing later it is on the other side.
            UseSwing();
            var one = CardRevealView.FallSwing(0.1f + 0.5f, 1f, 1f, 0f, _settings);
            var other = CardRevealView.FallSwing(0.1f + 0.9f, 1f, 1f, 0f, _settings);
            Assert.AreEqual(-one.x, other.x, 1e-4f);
            Assert.LessOrEqual(Mathf.Abs(one.x), 0.1f + Tolerance);
            Assert.LessOrEqual(Mathf.Abs(one.y), 10f + Tolerance);
        }

        [Test]
        public void FallSwing_ScalesWithThePopsReach()
        {
            UseSwing();
            var full = CardRevealView.FallSwing(0.7f, 1f, 1f, 0.5f, _settings);
            var half = CardRevealView.FallSwing(0.7f, 0.5f, 1f, 0.5f, _settings);
            Assert.AreEqual(full.x * 0.5f, half.x, 1e-4f);
            Assert.AreEqual(full.y * 0.5f, half.y, 1e-4f);
        }

        [Test]
        public void FallSpin_BeforeTheRubberTears_IsNothing()
        {
            UseSwing();
            Assert.AreEqual(0f, CardRevealView.FallSpin(0.09f, 120f, _settings));
        }

        [Test]
        public void FallSpin_PicksUpOverOneSwing_ThenTurnsAtItsRate()
        {
            // Torn at 0.1 s with a 0.4 s swing: just after the tear it has barely turned; well into the fall each
            // further second adds close to the full rate.
            UseSwing();
            Assert.Less(CardRevealView.FallSpin(0.15f, 120f, _settings), 1f);
            var late = CardRevealView.FallSpin(10.1f, 120f, _settings);
            var later = CardRevealView.FallSpin(11.1f, 120f, _settings);
            Assert.AreEqual(120f, later - late, 1f);
        }

        [Test]
        public void FallSpin_TurnsTheWayItsRatePoints()
        {
            UseSwing();
            Assert.AreEqual(-CardRevealView.FallSpin(1f, 120f, _settings),
                CardRevealView.FallSpin(1f, -120f, _settings), 1e-4f);
        }

        [Test]
        public void FallDistance_BeforeTheRubberTears_IsNothing()
        {
            _settings.RubberFallsFromFrame.Returns(3);
            _settings.FramesPerSecond.Returns(30f);
            _settings.RubberFallGravity.Returns(0.5f);
            Assert.AreEqual(0f, CardRevealView.FallDistance(0.09f, _settings), Tolerance);
        }

        [Test]
        public void FallDistance_AfterItTears_SpeedsUpUnderGravity()
        {
            // Torn at frame 3 of 30 a second: 12 frames of falling at 0.5 a frame squared is 36 frame heights, and
            // twice as long is four times.
            _settings.RubberFallsFromFrame.Returns(3);
            _settings.FramesPerSecond.Returns(30f);
            _settings.RubberFallGravity.Returns(0.5f);
            Assert.AreEqual(36f, CardRevealView.FallDistance(0.5f, _settings), 1e-3f);
            Assert.AreEqual(144f, CardRevealView.FallDistance(0.9f, _settings), 1e-3f);
        }

        [Test]
        public void FallDistance_AfterTheRecentring_AddsItsWholeHeight()
        {
            // Recentred from frame 20 over 10 frames by 0.3: by frame 30 (1 s) the fall's 0.5 x 0.01 x 27 squared
            // plus all of the 0.3; halfway through it, half of it.
            _settings.RubberFallsFromFrame.Returns(3);
            _settings.FramesPerSecond.Returns(30f);
            _settings.RubberFallGravity.Returns(0.01f);
            _settings.RubberRecentreFromFrame.Returns(20);
            _settings.RubberRecentreFrames.Returns(10);
            _settings.RubberRecentreHeight.Returns(0.3f);
            Assert.AreEqual(0.5f * 0.01f * 27f * 27f + 0.3f, CardRevealView.FallDistance(1f, _settings), 1e-4f);
            Assert.AreEqual(0.5f * 0.01f * 22f * 22f + 0.15f, CardRevealView.FallDistance(25f / 30f, _settings),
                1e-4f);
        }

        [Test]
        public void FallDistance_IsCountedInFrames_SoASlowerPopFallsSlower()
        {
            // The same frame reached at half the frame rate has fallen as far: the shreds trailing in the frames
            // keep matching the motion.
            _settings.RubberFallsFromFrame.Returns(3);
            _settings.RubberFallGravity.Returns(0.5f);
            _settings.FramesPerSecond.Returns(30f);
            var atFull = CardRevealView.FallDistance(0.5f, _settings);
            _settings.FramesPerSecond.Returns(15f);
            Assert.AreEqual(atFull, CardRevealView.FallDistance(1f, _settings), 1e-3f);
        }

        [Test]
        public void RibbonGrownScale_WithRoomToGrow_IsTheFullGrowth()
        {
            Assert.AreEqual(1.3f, RibbonPiece.GrownScale(1.3f, 100f, 205f), Tolerance);
        }

        [Test]
        public void RibbonGrownScale_ForALongLine_StopsAtTheWidestItMayBe()
        {
            // 180 wide may only grow to 205: a long description stays on screen.
            Assert.AreEqual(205f / 180f, RibbonPiece.GrownScale(1.3f, 180f, 205f), Tolerance);
        }

        [Test]
        public void RibbonGrownScale_ForALineAlreadyTooWide_NeverShrinksIt()
        {
            Assert.AreEqual(1f, RibbonPiece.GrownScale(1.3f, 240f, 205f), Tolerance);
        }

        [Test]
        public void RibbonWave_IsFlatAtItsNodes_AndPeaksAQuarterWaveOn()
        {
            Assert.AreEqual(0f, RibbonGraphic.Wave(0f, 0f, 2f, 40f, 1f), Tolerance);
            Assert.AreEqual(2f, RibbonGraphic.Wave(10f, 0f, 2f, 40f, 1f), Tolerance);
        }

        [Test]
        public void RibbonWave_RunsAlongTheRibbon()
        {
            // A quarter of a second at one wave a second moves the peak a quarter wave along.
            Assert.AreEqual(2f, RibbonGraphic.Wave(20f, 0.25f, 2f, 40f, 1f), Tolerance);
        }

        [Test]
        public void FirstFrontFrame_IsTheSheetsTopLeftCell()
        {
            AssertRect(new Rect(0f, 7f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(0, true, 6, 4));
        }

        [Test]
        public void FirstBackFrame_IsTheTopLeftCellOfTheBottomHalf()
        {
            AssertRect(new Rect(0f, 3f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(0, false, 6, 4));
        }

        [Test]
        public void Frames_ReadLeftToRight_ThenTopToBottom()
        {
            AssertRect(new Rect(5f / 6f, 7f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(5, true, 6, 4));
            AssertRect(new Rect(0f, 6f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(6, true, 6, 4));
        }

        [Test]
        public void LastFrames_EndAtEachHalfsBottomRightCell()
        {
            AssertRect(new Rect(5f / 6f, 4f / 8f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(23, true, 6, 4));
            AssertRect(new Rect(5f / 6f, 0f, 1f / 6f, 1f / 8f), CardRevealView.FrameRect(23, false, 6, 4));
        }

        private static void AssertRect(Rect expected, Rect actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, Tolerance, "y");
            Assert.AreEqual(expected.width, actual.width, Tolerance, "width");
            Assert.AreEqual(expected.height, actual.height, Tolerance, "height");
        }

        private void SetPin()
        {
            _settings.PinEnterSeconds.Returns(0.3f);
            _settings.PinEnterDistance.Returns(120f);
            _settings.PinHoverDistance.Returns(12f);
            _settings.PinWindupDistance.Returns(10f);
            _settings.PinWindupSeconds.Returns(0.2f);
            _settings.PinJabSeconds.Returns(0.1f);
            _settings.PinDepth.Returns(3f);
            _settings.PinStaySeconds.Returns(0.08f);
            _settings.PinRecoilDistance.Returns(36f);
            _settings.PinLeaveSeconds.Returns(0.4f);
        }

        private void SetFloat()
        {
            _settings.RiseSeconds.Returns(0.8f);
            _settings.RiseOvershoot.Returns(0.1f);
            _settings.RiseSway.Returns(6f);
            _settings.RiseSwaySeconds.Returns(0.5f);
            _settings.RiseTilt.Returns(5f);
        }

        private void UseSwing()
        {
            _settings.RubberFallsFromFrame.Returns(3);
            _settings.FramesPerSecond.Returns(30f);
            _settings.RubberSwing.Returns(0.1f);
            _settings.RubberSwingSeconds.Returns(0.4f);
            _settings.RubberSwingTilt.Returns(10f);
        }
    }
}
