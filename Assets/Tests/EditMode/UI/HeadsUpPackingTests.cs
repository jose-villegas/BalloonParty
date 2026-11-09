using System.Collections.Generic;
using BalloonParty.UI.HeadsUp;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class HeadsUpPackingTests
    {
        private static readonly Rect Bounds = new(-500f, -500f, 1000f, 1000f);
        private static readonly Vector2 LineSize = new(100f, 20f);

        [Test]
        public void ViewRect_IsWhatTheOrthographicCameraShows_AroundIt()
        {
            var go = new GameObject("ViewRectCamera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 4.8f;
                camera.aspect = 0.5f;
                go.transform.position = new Vector3(0.3f, -0.2f, -10f);

                var view = HeadsUpPacking.ViewRect(camera);

                Assert.That(view.center.x, Is.EqualTo(0.3f).Within(1e-5f));
                Assert.That(view.center.y, Is.EqualTo(-0.2f).Within(1e-5f));
                Assert.That(view.height, Is.EqualTo(9.6f).Within(1e-5f));
                Assert.That(view.width, Is.EqualTo(4.8f).Within(1e-5f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Place_NothingShowing_KeepsTheWantedSpot()
        {
            var placed = HeadsUpPacking.Place(Bounds, new Vector2(30f, 40f), LineSize, new List<Rect>());

            Assert.AreEqual(new Vector2(30f, 40f), placed);
        }

        [Test]
        public void Place_OnALineStillShowing_MovesOneStepClear()
        {
            var occupied = new List<Rect> { RectAt(Vector2.zero) };

            var placed = HeadsUpPacking.Place(Bounds, Vector2.zero, LineSize, occupied);

            Assert.LessOrEqual(HeadsUpPacking.WorstOverlap(placed, LineSize, occupied), HeadsUpPacking.AllowedOverlap);
            // The nearest ring: one line height up or down, or half a width sideways.
            Assert.LessOrEqual(Mathf.Abs(placed.y), LineSize.y + 1e-3f);
            Assert.LessOrEqual(Mathf.Abs(placed.x), LineSize.x * 0.5f + 1e-3f);
        }

        [Test]
        public void Place_AtTheEdge_StaysWhollyInside()
        {
            var placed = HeadsUpPacking.Place(Bounds, new Vector2(Bounds.xMax, 0f), LineSize, new List<Rect>());

            Assert.LessOrEqual(placed.x + LineSize.x * 0.5f, Bounds.xMax + 1e-3f);
        }

        [Test]
        public void Place_NoFreeSpotInReach_TakesTheLeastCovered()
        {
            // Covered up to y = 85: every reachable spot overlaps, least at the top ring (y = 80, a quarter clear).
            var occupied = new List<Rect> { Rect.MinMaxRect(-400f, -400f, 400f, 85f) };

            var placed = HeadsUpPacking.Place(Bounds, Vector2.zero, LineSize, occupied);

            Assert.AreEqual(4 * LineSize.y, placed.y, 1e-3f);
            Assert.AreEqual(0.75f, HeadsUpPacking.WorstOverlap(placed, LineSize, occupied), 1e-3f);
        }

        [Test]
        public void WorstOverlap_HalfCovered_IsAHalf()
        {
            var occupied = new List<Rect> { RectAt(new Vector2(LineSize.x * 0.5f, 0f)) };

            Assert.AreEqual(0.5f, HeadsUpPacking.WorstOverlap(Vector2.zero, LineSize, occupied), 1e-5f);
        }

        private static Rect RectAt(Vector2 center)
        {
            return new Rect(center - LineSize * 0.5f, LineSize);
        }
    }
}
