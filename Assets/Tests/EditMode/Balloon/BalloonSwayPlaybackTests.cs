using BalloonParty.Balloon.Controller;
using BalloonParty.Configuration.Balloons;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The idle sway's state machine on straight-line loops, so the angle of each loop at each step is
    ///     arithmetic: the stable loop reads 10 degrees a second from 0, the unstable one 10 a second from 100.
    ///     The rules under test are the Animator controller's, which <see cref="BalloonSwayPlayback" /> stands in
    ///     for; the shipped curves and the real Animator are compared elsewhere (<c>ShippedBalloonIdleSwayTests</c>,
    ///     <c>BalloonSwayAnimatorParityTests</c> and the PlayMode <c>BalloonSwayAnimatorParityPlayModeTests</c>).
    /// </summary>
    [TestFixture]
    public class BalloonSwayPlaybackTests
    {
        private const float Tight = 1e-4f;

        // Two loops mid-crossfade go through three trigonometric calls on angles past 100 degrees.
        private const float Mixed = 1e-3f;

        private const float StableLoopSeconds = 2f;
        private const float StableEnterSeconds = 0.5f;
        private const float StableSwitchSeconds = 1f;
        private const float UnstableLoopSeconds = 4f;
        private const float UnstableEnterSeconds = 0.25f;
        private const float UnstableSwitchSeconds = 2f;

        private IBalloonIdleSwaySettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = LinearSettings();
        }

        internal static IBalloonIdleSwaySettings LinearSettings()
        {
            var stable = new BalloonSwayLoop(
                AnimationCurve.Linear(0f, 0f, StableLoopSeconds, 10f * StableLoopSeconds),
                StableLoopSeconds, StableEnterSeconds, StableSwitchSeconds);
            var unstable = new BalloonSwayLoop(
                AnimationCurve.Linear(0f, 100f, UnstableLoopSeconds, 100f + 10f * UnstableLoopSeconds),
                UnstableLoopSeconds, UnstableEnterSeconds, UnstableSwitchSeconds);
            return new BalloonIdleSwaySettings(null, "Pivot", stable, unstable);
        }

        // The Animator weighs the two poses' quaternions, sums and normalises them. About one axis that is not
        // a straight line between the angles unless they are mirrored about the result.
        [Test]
        public void Mix_IsTheNormalisedSumOfTheTwoRotations_NotAStraightLineBetweenTheAngles()
        {
            Assert.AreEqual(21.59816f, BalloonSwayPlayback.Mix(0f, 90f, 0.25f), Tight, "a straight line gives 22.5");
            Assert.AreEqual(24.58890f, BalloonSwayPlayback.Mix(35f, -7f, 0.25f), Tight, "a straight line gives 24.5");
            Assert.AreEqual(-3.41110f, BalloonSwayPlayback.Mix(-35f, 7f, 0.75f), Tight, "a straight line gives -3.5");
            Assert.AreEqual(14f, BalloonSwayPlayback.Mix(35f, -7f, 0.5f), Tight, "halfway is halfway either way");
            Assert.AreEqual(35f, BalloonSwayPlayback.Mix(35f, -7f, 0f), Tight);
            Assert.AreEqual(-7f, BalloonSwayPlayback.Mix(35f, -7f, 1f), Tight);
        }

        [Test]
        public void FreshPlayback_PlaysTheUnstableLoopWholeFromItsFirstStep_AndIsEnteringItForItsEnterTime()
        {
            var playback = default(BalloonSwayPlayback);

            Assert.AreEqual(100f, playback.Advance(0.1f, _settings), Tight, "the loop's beginning, no fade in");
            Assert.AreEqual(101f, playback.Advance(0.1f, _settings), Tight);
            Assert.AreEqual(102f, playback.Advance(0.1f, _settings), Tight);
            Assert.IsTrue(playback.Blending, "0.2 s into the loop's 0.25 s enter time");

            Assert.AreEqual(103f, playback.Advance(0.1f, _settings), Tight);
            Assert.IsFalse(playback.Blending);
        }

        [Test]
        public void StableSetBeforeTheFirstStep_EntersTheStableLoopOverItsOwnEnterTime()
        {
            var playback = default(BalloonSwayPlayback);
            playback.Stable = true;

            Assert.AreEqual(0f, playback.Advance(0.25f, _settings), Tight);
            Assert.AreEqual(2.5f, playback.Advance(0.25f, _settings), Tight);
            Assert.IsTrue(playback.Blending, "0.25 s into the stable loop's 0.5 s enter time");

            Assert.AreEqual(5f, playback.Advance(0.25f, _settings), Tight);
            Assert.IsFalse(playback.Blending);
        }

        [Test]
        public void Phase_IsTheTimeSinceTheStepTheLoopWasAskedFor_AndWrapsAtItsLength()
        {
            var playback = default(BalloonSwayPlayback);
            playback.Advance(0.25f, _settings);

            Assert.AreEqual(102.5f, playback.Advance(0.25f, _settings), Tight);
            Assert.AreEqual(137.5f, playback.Advance(3.5f, _settings), Tight);
            Assert.AreEqual(102.5f, playback.Advance(0.5f, _settings), Tight, "4.25 s into a 4 s loop is 0.25 s");
            Assert.AreEqual(0.25f, playback.CurrentSeconds, Tight);
        }

        [Test]
        public void ZeroDeltaTime_HoldsThePose()
        {
            var playback = default(BalloonSwayPlayback);

            Assert.AreEqual(100f, playback.Advance(0f, _settings), Tight);
            Assert.AreEqual(100f, playback.Advance(0f, _settings), Tight);

            playback.Advance(1f, _settings);
            var held = playback.Advance(0f, _settings);
            Assert.AreEqual(110f, held, Tight);
            Assert.AreEqual(held, playback.Advance(0f, _settings), Tight);
        }

        [Test]
        public void Switch_StartsOnTheStepThatSeesIt_AtNoProgress_ThenCrossfadesIntoTheNewLoopFromItsStart()
        {
            var playback = SettledUnstable(1f);

            playback.Stable = true;

            Assert.AreEqual(112.5f, playback.Advance(0.25f, _settings), Mixed, "still all the old loop this step");
            Assert.IsTrue(playback.Blending);

            // Old loop at 1.5 s (115), new at 0.25 s (2.5), a quarter of the stable loop's 1 s crossfade.
            Assert.AreEqual(BalloonSwayPlayback.Mix(115f, 2.5f, 0.25f), playback.Advance(0.25f, _settings), Mixed);
            Assert.AreEqual(BalloonSwayPlayback.Mix(117.5f, 5f, 0.5f), playback.Advance(0.25f, _settings), Mixed);
            Assert.AreEqual(10f, playback.Advance(0.5f, _settings), Tight, "the crossfade is over: stable at 1 s");
            Assert.IsFalse(playback.Blending);
        }

        [Test]
        public void Switch_TakesTheEnteredLoopsCrossfadeTime()
        {
            var playback = SettledUnstable(1f);
            playback.Stable = true;
            playback.Advance(0.5f, _settings);
            Assert.AreEqual(10f, playback.Advance(1f, _settings), Tight);

            playback.Stable = false;
            Assert.AreEqual(12.5f, playback.Advance(0.25f, _settings), Mixed);

            // Stable at 1.75 s (17.5), unstable from its start at 0.5 s (105), a quarter of unstable's 2 s crossfade.
            Assert.AreEqual(BalloonSwayPlayback.Mix(17.5f, 105f, 0.25f), playback.Advance(0.5f, _settings), Mixed);
        }

        [Test]
        public void FlipBackDuringACrossfade_WaitsForItToEnd_ThenCrossfadesBackFromTheStepAfter()
        {
            var playback = SettledUnstable(1f);
            playback.Stable = true;
            playback.Advance(0.25f, _settings);
            Assert.AreEqual(BalloonSwayPlayback.Mix(115f, 2.5f, 0.25f), playback.Advance(0.25f, _settings), Mixed);

            playback.Stable = false;

            Assert.AreEqual(BalloonSwayPlayback.Mix(117.5f, 5f, 0.5f), playback.Advance(0.25f, _settings), Mixed,
                "the crossfade carries on untouched");
            Assert.AreEqual(10f, playback.Advance(0.5f, _settings), Tight, "and lands on the stable loop");
            Assert.IsFalse(playback.Blending, "the step a crossfade ends on starts nothing");

            Assert.AreEqual(12.5f, playback.Advance(0.25f, _settings), Mixed, "the way back starts here, at no progress");
            Assert.IsTrue(playback.Blending);

            // Stable at 1.5 s (15), unstable started over at 0.25 s (102.5), an eighth of its 2 s crossfade.
            Assert.AreEqual(BalloonSwayPlayback.Mix(15f, 102.5f, 0.125f), playback.Advance(0.25f, _settings), Mixed);
            Assert.AreEqual(0.25f, playback.NextSeconds, Tight);
        }

        [Test]
        public void FlipDuringTheEnterTime_WaitsForItToEnd()
        {
            var playback = default(BalloonSwayPlayback);
            playback.Advance(0.1f, _settings);
            Assert.AreEqual(101f, playback.Advance(0.1f, _settings), Tight);

            playback.Stable = true;

            Assert.AreEqual(102f, playback.Advance(0.1f, _settings), Tight, "still entering the unstable loop");
            Assert.AreEqual(103f, playback.Advance(0.1f, _settings), Tight, "entered; nothing starts on this step");
            Assert.AreEqual(104f, playback.Advance(0.1f, _settings), Mixed, "the crossfade starts, at no progress");

            // Unstable at 0.5 s (105), stable from its start at 0.1 s (1), a tenth of its 1 s crossfade.
            Assert.AreEqual(BalloonSwayPlayback.Mix(105f, 1f, 0.1f), playback.Advance(0.1f, _settings), Mixed);
        }

        // A playback past its enter time, `seconds` into the unstable loop.
        private BalloonSwayPlayback SettledUnstable(float seconds)
        {
            var playback = default(BalloonSwayPlayback);
            playback.Advance(0f, _settings);
            Assert.AreEqual(100f + 10f * seconds, playback.Advance(seconds, _settings), Tight);
            Assert.IsFalse(playback.Blending);
            return playback;
        }
    }
}
