using BalloonParty.EditorUI.Charts;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.EditorUI.Tests.Charts
{
    [TestFixture]
    public class PlotGridComputationTests
    {
        [Test]
        public void ComputeGridLines_DivisionsZero_ReturnsEmptyArray()
        {
            var lines = PlotGrid.ComputeGridLines(new Rect(0f, 0f, 100f, 40f), 0, 0f, 10f);

            Assert.That(lines, Is.Empty);
        }

        [Test]
        public void ComputeGridLines_EqualMinAndMax_ReturnsSingleCenteredLine()
        {
            var area = new Rect(10f, 20f, 60f, 40f);

            var lines = PlotGrid.ComputeGridLines(area, 4, 2.5f, 2.5f);

            Assert.That(lines, Has.Length.EqualTo(1));
            Assert.That(lines[0].Y, Is.EqualTo(area.center.y).Within(0.001f));
            Assert.That(lines[0].Label, Is.EqualTo("2.5"));
        }

        [Test]
        public void ComputeGridLines_NormalRange_ReturnsExpectedCountSpacingAndLabels()
        {
            var area = new Rect(0f, 10f, 100f, 90f);

            var lines = PlotGrid.ComputeGridLines(area, 2, 0f, 10f);

            Assert.That(lines, Has.Length.EqualTo(3));
            Assert.That(lines[0].Y, Is.EqualTo(100f).Within(0.001f));
            Assert.That(lines[1].Y, Is.EqualTo(55f).Within(0.001f));
            Assert.That(lines[2].Y, Is.EqualTo(10f).Within(0.001f));
            Assert.That(lines[0].Label, Is.EqualTo("0"));
            Assert.That(lines[1].Label, Is.EqualTo("5"));
            Assert.That(lines[2].Label, Is.EqualTo("10"));
        }
    }
}
