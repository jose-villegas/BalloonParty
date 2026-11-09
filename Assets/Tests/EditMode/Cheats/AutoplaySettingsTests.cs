#if UNITY_ENABLE_CHECKS
using BalloonParty.Cheats.Autoplay;
using NUnit.Framework;

namespace BalloonParty.Tests.Cheats
{
    [TestFixture]
    public class AutoplaySettingsTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void TryParse_NoSpec_IsNoAutoplay(string spec)
        {
            Assert.That(AutoplaySettings.TryParse(spec, out var settings), Is.False);
            Assert.That(settings, Is.Null);
        }

        [Test]
        public void TryParse_TheRunnersFullSpec_ReadsEveryKnob()
        {
            var parsed = AutoplaySettings.TryParse(
                "player=human seconds=420 restart=1 floor=0.15 mean=0.25 aim=1.5 wild=0 seed=7", out var settings);

            Assert.That(parsed, Is.True);
            Assert.That(settings.Best, Is.False);
            Assert.That(settings.Seconds, Is.EqualTo(420f));
            Assert.That(settings.Restart, Is.True);
            Assert.That(settings.ReactFloor, Is.EqualTo(0.15f));
            Assert.That(settings.ReactMean, Is.EqualTo(0.25f));
            Assert.That(settings.AimSd, Is.EqualTo(1.5f));
            Assert.That(settings.WildChance, Is.EqualTo(0f));
            Assert.That(settings.Seed, Is.EqualTo(7));
        }

        [Test]
        public void TryParse_OnlyThePlayer_KeepsTheAverageHumanDefaults()
        {
            Assert.That(AutoplaySettings.TryParse("player=best", out var settings), Is.True);

            Assert.That(settings.Best, Is.True);
            Assert.That(settings.Seconds, Is.EqualTo(240f));
            Assert.That(settings.Restart, Is.False);
            Assert.That(settings.Seed, Is.Null);
        }

        [TestCase("player=robot")]
        [TestCase("seconds=0")]
        [TestCase("seconds=-5")]
        [TestCase("restart=yes")]
        [TestCase("wild=1.5")]
        [TestCase("aim=-1")]
        [TestCase("seed=x")]
        [TestCase("speed=2")]
        [TestCase("player")]
        [TestCase("=human")]
        public void TryParse_AMalformedSpec_IsRejectedRatherThanPlayedWrong(string spec)
        {
            Assert.That(AutoplaySettings.TryParse(spec, out var settings), Is.False);
            Assert.That(settings, Is.Null);
        }

        [Test]
        public void TryParse_ReadsDecimalPointsWhateverTheLocale()
        {
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

                Assert.That(AutoplaySettings.TryParse("mean=0.6", out var settings), Is.True);
                Assert.That(settings.ReactMean, Is.EqualTo(0.6f));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        [Test]
        public void ReactionDelay_IsTheFloorAtAZeroDraw_AndGrowsWithTheDraw()
        {
            Assert.That(AutoplayDice.ReactionDelay(0.25f, 0.6f, 0f), Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(AutoplayDice.ReactionDelay(0.25f, 0.6f, 0.5f),
                Is.EqualTo(0.25f + 0.6f * System.Math.Log(2.0)).Within(1e-5f));
            Assert.That(AutoplayDice.ReactionDelay(0.25f, 0.6f, 0.99f),
                Is.GreaterThan(AutoplayDice.ReactionDelay(0.25f, 0.6f, 0.5f)));
        }

        [Test]
        public void ReactionDelay_AveragesTheFloorPlusTheMean()
        {
            const int draws = 20000;
            var sum = 0.0;
            for (var i = 0; i < draws; i++)
            {
                sum += AutoplayDice.ReactionDelay(0.25f, 0.6f, (i + 0.5f) / draws);
            }

            Assert.That(sum / draws, Is.EqualTo(0.85).Within(0.01));
        }

        [Test]
        public void Normal_IsFiniteAtTheEdgesOfItsDraws()
        {
            Assert.That(float.IsFinite(AutoplayDice.Normal(0f, 0f)), Is.True);
            Assert.That(float.IsFinite(AutoplayDice.Normal(0.999999f, 0.5f)), Is.True);
        }

        [Test]
        public void Normal_HasMeanZeroAndUnitSpread()
        {
            var random = new System.Random(3);
            const int draws = 20000;
            double sum = 0.0, squares = 0.0;
            for (var i = 0; i < draws; i++)
            {
                double value = AutoplayDice.Normal((float)random.NextDouble(), (float)random.NextDouble());
                sum += value;
                squares += value * value;
            }

            var mean = sum / draws;
            Assert.That(mean, Is.EqualTo(0.0).Within(0.05));
            Assert.That(squares / draws - mean * mean, Is.EqualTo(1.0).Within(0.05));
        }
    }
}
#endif
