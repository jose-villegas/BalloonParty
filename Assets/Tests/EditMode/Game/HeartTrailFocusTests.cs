using System.Collections.Generic;
using BalloonParty.Game.Cinematics;
using BalloonParty.Game.Health;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class HeartTrailFocusTests
    {
        private readonly List<GameObject> _objects = new();
        private HeartTrailTracker _tracker;
        private HeartTrailFocus _focus;

        [SetUp]
        public void SetUp()
        {
            _tracker = new HeartTrailTracker();
            _focus = new HeartTrailFocus(_tracker);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                Object.DestroyImmediate(go);
            }

            _objects.Clear();
        }

        [Test]
        public void NoTrails_NoFocus()
        {
            Assert.IsFalse(_focus.TryGetFocus(out _, out _, out _));
        }

        [Test]
        public void CenterIsTheOldestTrail_NotTheCentroid()
        {
            // The oldest heart is the one about to land and pop — the beat the camera must not lose.
            // A centroid would drift toward the newer (UI-side) trails.
            AddTrail(new Vector3(0f, -5f, 0f));
            AddTrail(new Vector3(0f, 10f, 0f));
            AddTrail(new Vector3(0f, 12f, 0f));

            Assert.IsTrue(_focus.TryGetFocus(out var center, out _, out _));
            Assert.AreEqual(new Vector3(0f, -5f, 0f), center);
        }

        [Test]
        public void BoundsSpanAllTrails()
        {
            AddTrail(new Vector3(-2f, -5f, 0f));
            AddTrail(new Vector3(4f, 10f, 0f));

            Assert.IsTrue(_focus.TryGetFocus(out _, out var min, out var max));
            Assert.AreEqual(new Vector3(-2f, -5f, 0f), min);
            Assert.AreEqual(new Vector3(4f, 10f, 0f), max);
        }

        [Test]
        public void OldestArriving_HandsFocusToTheNext()
        {
            var first = AddTrail(new Vector3(0f, -5f, 0f));
            AddTrail(new Vector3(0f, 8f, 0f));

            _tracker.Remove(first.transform);

            Assert.IsTrue(_focus.TryGetFocus(out var center, out _, out _));
            Assert.AreEqual(new Vector3(0f, 8f, 0f), center);
        }

        [Test]
        public void DestroyedTrail_IsSkippedWithoutLosingFocus()
        {
            var first = AddTrail(new Vector3(0f, -5f, 0f));
            AddTrail(new Vector3(0f, 8f, 0f));

            // Destroyed but not yet removed from the tracker (a pooled teardown race).
            Object.DestroyImmediate(first);

            Assert.IsTrue(_focus.TryGetFocus(out var center, out _, out _));
            Assert.AreEqual(new Vector3(0f, 8f, 0f), center);
        }

        private GameObject AddTrail(Vector3 position)
        {
            var go = new GameObject("trail");
            go.transform.position = position;
            _objects.Add(go);
            _tracker.Add(go.transform);
            return go;
        }
    }
}
