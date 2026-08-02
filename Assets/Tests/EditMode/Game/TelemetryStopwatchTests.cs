using BalloonParty.Game.Telemetry;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class TelemetryStopwatchTests
    {
        private float _now;
        private TelemetryStopwatch _stopwatch;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            _stopwatch = new TelemetryStopwatch(() => _now);
        }

        [Test]
        public void Elapsed_BeforeResume_IsZero()
        {
            _now = 10f;

            Assert.AreEqual(0f, _stopwatch.Elapsed);
        }

        [Test]
        public void Elapsed_WhileRunning_AccumulatesClockAdvance()
        {
            _stopwatch.Resume();
            _now = 3f;

            Assert.AreEqual(3f, _stopwatch.Elapsed);
        }

        [Test]
        public void Elapsed_WhilePaused_DoesNotAccumulate()
        {
            _stopwatch.Resume();
            _now = 2f;
            _stopwatch.Pause();
            _now = 10f;

            Assert.AreEqual(2f, _stopwatch.Elapsed);
        }

        [Test]
        public void Resume_CalledTwice_IsIdempotent()
        {
            _stopwatch.Resume();
            _now = 1f;
            _stopwatch.Resume(); // must not resample and discard the elapsed second
            _now = 2f;

            Assert.AreEqual(2f, _stopwatch.Elapsed);
        }

        [Test]
        public void Pause_CalledTwice_IsIdempotent()
        {
            _stopwatch.Resume();
            _now = 4f;
            _stopwatch.Pause();
            _stopwatch.Pause();

            Assert.AreEqual(4f, _stopwatch.Elapsed);
        }

        [Test]
        public void Reset_WhileStopped_StaysStoppedAndZeroesElapsed()
        {
            _stopwatch.Resume();
            _now = 4f;
            _stopwatch.Pause();

            _stopwatch.Reset();

            Assert.IsFalse(_stopwatch.IsRunning);
            Assert.AreEqual(0f, _stopwatch.Elapsed);

            // The clock keeps moving; a stopped stopwatch must not pick it back up on its own.
            _now = 9f;
            Assert.AreEqual(0f, _stopwatch.Elapsed);
        }

        // Reset() used to force every timer to a stopped state. The Wall clock spans a whole level and
        // is never explicitly paused/resumed at the flush boundary (only Gameplay is), so that old
        // behaviour silently lost Wall from the second level onward — see
        // PLAN-GameplayTelemetry.md's flush sequence and "Cheap hardening" in the W1 rework.
        [Test]
        public void Reset_WhileRunning_StaysRunningAndZeroesElapsed()
        {
            _stopwatch.Resume();
            _now = 4f;

            _stopwatch.Reset();

            Assert.IsTrue(_stopwatch.IsRunning);
            Assert.AreEqual(0f, _stopwatch.Elapsed);

            // Still running across the reset, so the clock keeps accumulating from zero.
            _now = 7f;
            Assert.AreEqual(3f, _stopwatch.Elapsed);
        }

        [Test]
        public void BackwardsClock_ClampsToZeroDelta_InsteadOfCorruptingElapsed()
        {
            _stopwatch.Resume();
            _now = 5f;
            Assert.AreEqual(5f, _stopwatch.Elapsed);

            _now = 2f; // the clock jumped backwards
            Assert.AreEqual(5f, _stopwatch.Elapsed, "a backwards jump must not subtract from Elapsed");

            _now = 4f; // resumes forward from the resynced (backwards) sample
            Assert.AreEqual(7f, _stopwatch.Elapsed);
        }

        [Test]
        public void RepeatedElapsedReads_WithoutClockMovement_AreStable()
        {
            _stopwatch.Resume();
            _now = 3f;

            var first = _stopwatch.Elapsed;
            var second = _stopwatch.Elapsed;
            var third = _stopwatch.Elapsed;

            Assert.AreEqual(first, second);
            Assert.AreEqual(second, third);
            Assert.AreEqual(3f, third);
        }
    }
}
