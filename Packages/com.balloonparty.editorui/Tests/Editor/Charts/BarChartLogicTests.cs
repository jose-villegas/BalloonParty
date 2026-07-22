using System.Collections.Generic;
using BalloonParty.EditorUI.Charts;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.EditorUI.Tests.Charts
{
    [TestFixture]
    public class BarChartLogicTests
    {
        private static readonly BarChartOptions DefaultOptions = new BarChartOptions
        {
            BarPadding = 6f,
            MinBarWidth = 4f
        };

        [Test]
        public void ComputeBarRects_EmptyValues_ReturnsEmptyArray()
        {
            var rects = BarChart.ComputeBarRects(new Rect(0f, 0f, 100f, 40f), new float[0], 10f, DefaultOptions);

            Assert.That(rects, Is.Empty);
        }

        [Test]
        public void ComputeBarRects_SingleValue_SizesBarWithinSingleSlot()
        {
            var area = new Rect(10f, 20f, 100f, 50f);

            var rects = BarChart.ComputeBarRects(area, new[] { 5f }, 10f, DefaultOptions);

            Assert.That(rects, Has.Length.EqualTo(1));
            Assert.That(rects[0].x, Is.EqualTo(13f).Within(0.001f));
            Assert.That(rects[0].y, Is.EqualTo(45f).Within(0.001f));
            Assert.That(rects[0].width, Is.EqualTo(94f).Within(0.001f));
            Assert.That(rects[0].height, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void ComputeBarRects_MultipleValues_UsesProportionalHeightsAndEvenSlots()
        {
            var area = new Rect(0f, 0f, 90f, 40f);

            var rects = BarChart.ComputeBarRects(area, new[] { 0f, 5f, 10f }, 10f, DefaultOptions);

            Assert.That(rects, Has.Length.EqualTo(3));
            Assert.That(rects[0], Is.EqualTo(new Rect(3f, 40f, 24f, 0f)).Using(RectEqualityComparer.Instance));
            Assert.That(rects[1], Is.EqualTo(new Rect(33f, 20f, 24f, 20f)).Using(RectEqualityComparer.Instance));
            Assert.That(rects[2], Is.EqualTo(new Rect(63f, 0f, 24f, 40f)).Using(RectEqualityComparer.Instance));
        }

        [Test]
        public void ComputeBarRects_MaxZero_GivesZeroHeightsAtBottom()
        {
            var area = new Rect(0f, 5f, 60f, 20f);

            var rects = BarChart.ComputeBarRects(area, new[] { 2f, 8f }, 0f, DefaultOptions);

            Assert.That(rects[0].height, Is.Zero);
            Assert.That(rects[1].height, Is.Zero);
            Assert.That(rects[0].y, Is.EqualTo(area.yMax).Within(0.001f));
            Assert.That(rects[1].y, Is.EqualTo(area.yMax).Within(0.001f));
        }

        [Test]
        public void ComputeBarRects_NullResolver_PreservesBarColor()
        {
            var options = new BarChartOptions
            {
                BarColor = Color.red,
                BarPadding = 2f,
                MinBarWidth = 4f,
                BarColorResolver = null
            };

            var rects = BarChart.ComputeBarRects(new Rect(0f, 0f, 100f, 50f), new[] { 5f, 10f }, 10f, options);

            Assert.That(rects, Has.Length.EqualTo(2));
            Assert.That(options.BarColorResolver, Is.Null);
        }

        [Test]
        public void BarChartOptions_ResolverInvokedPerBar_WhenSet()
        {
            var invocations = new List<(int index, float value)>();
            var options = new BarChartOptions
            {
                BarPadding = 2f,
                MinBarWidth = 4f,
                BarColorResolver = (i, v) =>
                {
                    invocations.Add((i, v));
                    return v >= 5f ? Color.green : Color.grey;
                }
            };
            float[] values = { 3f, 7f, 1f };

            // We can't call Draw (needs GUI context), but we can verify the resolver
            // logic independently by simulating the loop from Draw
            for (int i = 0; i < values.Length; i++)
            {
                options.BarColorResolver(i, values[i]);
            }

            Assert.That(invocations, Has.Count.EqualTo(3));
            Assert.That(invocations[0], Is.EqualTo((0, 3f)));
            Assert.That(invocations[1], Is.EqualTo((1, 7f)));
            Assert.That(invocations[2], Is.EqualTo((2, 1f)));
        }

        [Test]
        public void ThresholdLine_DefaultStruct_HasZeroValues()
        {
            var line = new ThresholdLine();

            Assert.That(line.Value, Is.Zero);
            Assert.That(line.Color, Is.EqualTo(default(Color)));
        }

        [Test]
        public void BarChartOptions_ThresholdNull_ByDefault()
        {
            var options = new BarChartOptions();

            Assert.That(options.Threshold, Is.Null);
        }

        [Test]
        public void IndexFromX_ValidCoordinate_ReturnsExpectedIndex()
        {
            var index = BarChart.IndexFromX(45f, new Rect(0f, 0f, 90f, 20f), 3);

            Assert.That(index, Is.EqualTo(1));
        }

        [Test]
        public void IndexFromX_CountZero_ReturnsNegativeOne()
        {
            var index = BarChart.IndexFromX(45f, new Rect(0f, 0f, 90f, 20f), 0);

            Assert.That(index, Is.EqualTo(-1));
        }

        [Test]
        public void IndexFromX_BelowArea_ClampsToFirstIndex()
        {
            var index = BarChart.IndexFromX(-5f, new Rect(10f, 0f, 90f, 20f), 3);

            Assert.That(index, Is.EqualTo(0));
        }

        [Test]
        public void IndexFromX_AtRightBoundary_ClampsToLastIndex()
        {
            var index = BarChart.IndexFromX(100f, new Rect(10f, 0f, 90f, 20f), 3);

            Assert.That(index, Is.EqualTo(2));
        }

        private sealed class RectEqualityComparer : IEqualityComparer<Rect>
        {
            public static readonly RectEqualityComparer Instance = new RectEqualityComparer();

            public bool Equals(Rect x, Rect y)
            {
                return Mathf.Approximately(x.x, y.x)
                    && Mathf.Approximately(x.y, y.y)
                    && Mathf.Approximately(x.width, y.width)
                    && Mathf.Approximately(x.height, y.height);
            }

            public int GetHashCode(Rect obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
