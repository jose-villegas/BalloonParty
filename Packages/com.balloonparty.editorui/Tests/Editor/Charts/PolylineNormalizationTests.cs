using System.Collections.Generic;
using BalloonParty.EditorUI.Charts;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.EditorUI.Tests.Charts
{
    [TestFixture]
    public class PolylineNormalizationTests
    {
        [Test]
        public void NormalizePoints_EmptyValues_ReturnsEmptyArray()
        {
            var points = PolylineOverlay.NormalizePoints(new Rect(0f, 0f, 30f, 20f), new float[0], 10f);

            Assert.That(points, Is.Empty);
        }

        [Test]
        public void NormalizePoints_SingleValue_ReturnsSinglePointAtLeftEdge()
        {
            var points = PolylineOverlay.NormalizePoints(new Rect(10f, 20f, 60f, 40f), new[] { 5f }, 10f);

            Assert.That(points, Has.Length.EqualTo(1));
            Assert.That(points[0].x, Is.EqualTo(10f).Within(0.001f));
            Assert.That(points[0].y, Is.EqualTo(40f).Within(0.001f));
        }

        [Test]
        public void NormalizePoints_AllSameValues_MapsToFlatLine()
        {
            var points = PolylineOverlay.NormalizePoints(new Rect(0f, 10f, 20f, 30f), new[] { 2f, 2f, 2f }, 2f);

            Assert.That(points, Has.Length.EqualTo(3));
            Assert.That(points[0], Is.EqualTo(new Vector2(0f, 10f)).Using(Vector2EqualityComparer.Instance));
            Assert.That(points[1], Is.EqualTo(new Vector2(10f, 10f)).Using(Vector2EqualityComparer.Instance));
            Assert.That(points[2], Is.EqualTo(new Vector2(20f, 10f)).Using(Vector2EqualityComparer.Instance));
        }

        [Test]
        public void NormalizePoints_MaxZero_PlacesAllPointsOnBottomEdge()
        {
            var plotArea = new Rect(5f, 15f, 20f, 30f);

            var points = PolylineOverlay.NormalizePoints(plotArea, new[] { 2f, 4f, 6f }, 0f);

            Assert.That(points, Has.Length.EqualTo(3));
            Assert.That(points[0].y, Is.EqualTo(plotArea.yMax).Within(0.001f));
            Assert.That(points[1].y, Is.EqualTo(plotArea.yMax).Within(0.001f));
            Assert.That(points[2].y, Is.EqualTo(plotArea.yMax).Within(0.001f));
        }

        [Test]
        public void NormalizePoints_NormalValues_MapsLinearlyAcrossArea()
        {
            var points = PolylineOverlay.NormalizePoints(new Rect(0f, 0f, 20f, 20f), new[] { 0f, 5f, 10f }, 10f);

            Assert.That(points, Has.Length.EqualTo(3));
            Assert.That(points[0], Is.EqualTo(new Vector2(0f, 20f)).Using(Vector2EqualityComparer.Instance));
            Assert.That(points[1], Is.EqualTo(new Vector2(10f, 10f)).Using(Vector2EqualityComparer.Instance));
            Assert.That(points[2], Is.EqualTo(new Vector2(20f, 0f)).Using(Vector2EqualityComparer.Instance));
        }

        private sealed class Vector2EqualityComparer : IEqualityComparer<Vector2>
        {
            public static readonly Vector2EqualityComparer Instance = new Vector2EqualityComparer();

            public bool Equals(Vector2 x, Vector2 y)
            {
                return Mathf.Approximately(x.x, y.x) && Mathf.Approximately(x.y, y.y);
            }

            public int GetHashCode(Vector2 obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
