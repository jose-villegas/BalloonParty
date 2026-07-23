using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Thermal;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class ThermalFrameRateGovernorTests
    {
        // Small values (vs. the shipped 10s/60s/90s tuning) so a sustained window is a handful of
        // 1-second Advance() calls instead of dozens; PollIntervalSeconds = 1 keeps each call its
        // own evaluation.
        private const float DownSustainSeconds = 3f;
        private const float UpSustainSeconds = 3f;
        private const float MinDwellSeconds = 6f;

        private FakeThermalSource _source;
        private ThermalFrameRateGovernor _governor;

        [SetUp]
        public void SetUp()
        {
            var settings = Substitute.For<IThermalGovernorSettings>();
            settings.Enabled.Returns(true);
            settings.RateLadder.Returns(new List<int> { 120, 80, 60 });
            settings.DownHeadroom.Returns(0.85f);
            settings.UpHeadroom.Returns(0.65f);
            settings.DownSustainSeconds.Returns(DownSustainSeconds);
            settings.UpSustainSeconds.Returns(UpSustainSeconds);
            settings.MinDwellSeconds.Returns(MinDwellSeconds);
            settings.PollIntervalSeconds.Returns(1f);

            _source = new FakeThermalSource();
            _governor = new ThermalFrameRateGovernor(settings, _source);
        }

        [Test]
        public void Advance_SustainedHeat_StepsDownOneRung()
        {
            _source.SetHot();

            _governor.Advance(1f);
            _governor.Advance(1f);
            Assert.AreEqual(0, _governor.CurrentRungIndex, "should not step before DownSustainSeconds elapses");

            _governor.Advance(1f); // 3rd hot second reaches DownSustainSeconds

            Assert.AreEqual(1, _governor.CurrentRungIndex);
            Assert.AreEqual(80, _governor.CurrentRate);
        }

        [Test]
        public void Advance_ContinuedSustainedHeat_StepsDownAgain()
        {
            _source.SetHot();

            AdvanceSeconds(3); // 120 -> 80
            AdvanceSeconds(3); // 80 -> 60

            Assert.AreEqual(2, _governor.CurrentRungIndex);
            Assert.AreEqual(60, _governor.CurrentRate);
        }

        [Test]
        public void Advance_SustainedHeatAtFloorRung_NeverStepsBelowFloor()
        {
            _source.SetHot();

            AdvanceSeconds(3);
            AdvanceSeconds(3);
            AdvanceSeconds(3); // already at the ladder floor; further heat must be a no-op

            Assert.AreEqual(2, _governor.CurrentRungIndex);
            Assert.AreEqual(60, _governor.CurrentRate);
        }

        [Test]
        public void Advance_TransientHeatSpike_DoesNotStepDown()
        {
            _source.SetHot();
            _governor.Advance(1f);
            _governor.Advance(1f); // 2s hot, below the 3s threshold

            _source.SetNeutral();
            _governor.Advance(1f); // sustain counter must reset here, not just stall

            _source.SetHot();
            _governor.Advance(1f);
            _governor.Advance(1f); // another 2s hot; would total 4s (>= threshold) if not reset

            Assert.AreEqual(0, _governor.CurrentRungIndex);
            Assert.AreEqual(120, _governor.CurrentRate);
        }

        [Test]
        public void Advance_CoolSustainedButDwellNotElapsed_DoesNotStepUp()
        {
            _source.SetHot();
            AdvanceSeconds(3); // step down to rung 1 (80)
            Assert.AreEqual(1, _governor.CurrentRungIndex);

            _source.SetCool();
            AdvanceSeconds(3); // UpSustainSeconds (3) satisfied, MinDwellSeconds (6) is not

            Assert.AreEqual(1, _governor.CurrentRungIndex, "must not step up on sustain alone, before dwell elapses");
        }

        [Test]
        public void Advance_CoolSustainedAndDwellElapsed_StepsUp()
        {
            _source.SetHot();
            AdvanceSeconds(3); // step down to rung 1 (80)

            _source.SetCool();
            AdvanceSeconds(6); // both UpSustainSeconds (3) and MinDwellSeconds (6) satisfied

            Assert.AreEqual(0, _governor.CurrentRungIndex);
            Assert.AreEqual(120, _governor.CurrentRate);
        }

        [Test]
        public void Advance_AlternatingHotCool_NeverAccumulatesEitherSustainWindow()
        {
            // Each poll flips the reading, so neither _downSustained nor _upSustained ever exceeds
            // 1s consecutively (well under the 3s thresholds) — the rung must not oscillate.
            for (var i = 0; i < 10; i++)
            {
                _source.SetHot();
                _governor.Advance(1f);

                _source.SetCool();
                _governor.Advance(1f);
            }

            Assert.AreEqual(0, _governor.CurrentRungIndex);
            Assert.AreEqual(120, _governor.CurrentRate);
        }

        [Test]
        public void Advance_Disabled_NeverChangesRungRegardlessOfHeat()
        {
            var settings = Substitute.For<IThermalGovernorSettings>();
            settings.Enabled.Returns(false);
            settings.RateLadder.Returns(new List<int> { 120, 80, 60 });
            settings.DownHeadroom.Returns(0.85f);
            settings.DownSustainSeconds.Returns(DownSustainSeconds);
            settings.PollIntervalSeconds.Returns(1f);
            var governor = new ThermalFrameRateGovernor(settings, _source);

            _source.SetHot();
            for (var i = 0; i < 10; i++)
            {
                governor.Advance(1f);
            }

            Assert.AreEqual(0, governor.CurrentRungIndex);
        }

        // PollIntervalSeconds is 1f exactly, so each 1-second Advance() call flushes its own
        // evaluation instead of accumulating across calls.
        private void AdvanceSeconds(int seconds)
        {
            for (var i = 0; i < seconds; i++)
            {
                _governor.Advance(1f);
            }
        }

        private class FakeThermalSource : IThermalSource
        {
            public float Headroom { get; private set; }
            public int Status { get; private set; }

            public void SetHot()
            {
                Headroom = 0.9f; // >= DownHeadroom (0.85)
                Status = 0;
            }

            public void SetCool()
            {
                Headroom = 0.6f; // <= UpHeadroom (0.65)
                Status = 0;
            }

            public void SetNeutral()
            {
                Headroom = 0.75f; // between UpHeadroom and DownHeadroom: neither hot nor cool
                Status = 0;
            }
        }
    }
}
