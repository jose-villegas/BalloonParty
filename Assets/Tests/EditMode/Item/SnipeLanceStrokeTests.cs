using BalloonParty.Item.Snipe;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class SnipeLanceStrokeTests
    {
        private static readonly SnipeLanceStrokeTiming Timing = new(0.2f, 0.5f, 0.4f);

        [Test]
        public void Evaluate_AtTheStart_NothingDrawnYetAtFullStrength()
        {
            Assert.IsTrue(SnipeLanceStroke.Evaluate(0f, Timing, out var drawn, out var alpha));
            Assert.AreEqual(0f, drawn, 1e-5f);
            Assert.AreEqual(1f, alpha, 1e-5f);
        }

        // Eased out: the tip covers more than half the run in the first half of the draw.
        [Test]
        public void Evaluate_HalfwayThroughTheDraw_TipIsPastHalfway()
        {
            SnipeLanceStroke.Evaluate(0.1f, Timing, out var drawn, out var alpha);

            Assert.Greater(drawn, 0.5f);
            Assert.Less(drawn, 1f);
            Assert.AreEqual(1f, alpha, 1e-5f);
        }

        [Test]
        public void Evaluate_DuringTheHold_WholeRunDrawnAtFullStrength()
        {
            Assert.IsTrue(SnipeLanceStroke.Evaluate(0.5f, Timing, out var drawn, out var alpha));
            Assert.AreEqual(1f, drawn, 1e-5f);
            Assert.AreEqual(1f, alpha, 1e-5f);
        }

        [Test]
        public void Evaluate_HalfwayThroughTheFade_HalfStrength()
        {
            SnipeLanceStroke.Evaluate(0.9f, Timing, out var drawn, out var alpha);

            Assert.AreEqual(1f, drawn, 1e-5f);
            Assert.AreEqual(0.5f, alpha, 1e-4f);
        }

        [Test]
        public void Evaluate_PastTheFade_FinishedAndInvisible()
        {
            Assert.IsFalse(SnipeLanceStroke.Evaluate(1.2f, Timing, out _, out var alpha));
            Assert.AreEqual(0f, alpha, 1e-5f);
        }

        [Test]
        public void Evaluate_ZeroDrawTime_WholeRunAppearsAtOnce()
        {
            var instant = new SnipeLanceStrokeTiming(0f, 0.5f, 0.4f);

            SnipeLanceStroke.Evaluate(0f, instant, out var drawn, out _);

            Assert.AreEqual(1f, drawn, 1e-5f);
        }
    }
}
