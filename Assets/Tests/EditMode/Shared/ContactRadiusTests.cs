using System.Collections.Generic;
using BalloonParty.Shared;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>Tests <see cref="ContactRadius.FromCollider" /> — the shared derivation (@ref
    /// plan_shot_solver_accuracy §3 "Shared radius helper") every projectile-facing collider read
    /// (live gather, Phase A's static archetypes, Phase G's synthetic gather) collapses onto. Pure
    /// math with three branches; needs a real <see cref="Collider2D" /> component, so it constructs
    /// throwaway <see cref="GameObject" />s (mirrors <c>SweepCruiseIsolationTests</c>'/
    /// <c>HeartTrailFocusTests</c>' EditMode precedent) and destroys them in <see cref="TearDown" />.</summary>
    [TestFixture]
    public class ContactRadiusTests
    {
        private readonly List<GameObject> _gameObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _gameObjects)
            {
                Object.DestroyImmediate(go);
            }

            _gameObjects.Clear();
        }

        [Test]
        public void FromCollider_CircleCollider_ReturnsRadiusScaledByLossyScale()
        {
            var collider = CreateCollider<CircleCollider2D>();
            collider.radius = 0.4f;

            var result = ContactRadius.FromCollider(collider, lossyScaleX: 2f);

            Assert.AreEqual(0.8f, result, 0.0001f);
        }

        [Test]
        public void FromCollider_CapsuleCollider_ReturnsHalfTheSmallerSizeAxisScaledByLossyScale()
        {
            var collider = CreateCollider<CapsuleCollider2D>();
            collider.size = new Vector2(0.6f, 1.2f);

            var result = ContactRadius.FromCollider(collider, lossyScaleX: 3f);

            Assert.AreEqual(0.9f, result, 0.0001f, "min(0.6, 1.2) * 0.5 * 3 = 0.9 — the cross-section half-extent");
        }

        [Test]
        public void FromCollider_CapsuleCollider_UsesWhicheverSizeAxisIsSmaller()
        {
            var collider = CreateCollider<CapsuleCollider2D>();
            collider.size = new Vector2(2f, 0.5f);

            var result = ContactRadius.FromCollider(collider, lossyScaleX: 1f);

            Assert.AreEqual(0.25f, result, 0.0001f, "min(2, 0.5) * 0.5 = 0.25 regardless of axis order");
        }

        [Test]
        public void FromCollider_UnsupportedColliderShape_ReturnsZero()
        {
            var collider = CreateCollider<BoxCollider2D>();

            var result = ContactRadius.FromCollider(collider, lossyScaleX: 5f);

            Assert.AreEqual(0f, result, "an unhandled collider shape is collision-inert, not a crash");
        }

        [Test]
        public void FromCollider_NullCollider_ReturnsZero()
        {
            var result = ContactRadius.FromCollider(null, lossyScaleX: 1f);

            Assert.AreEqual(0f, result, "no collider authored yet — feature stays inert rather than throwing");
        }

        private T CreateCollider<T>() where T : Collider2D
        {
            var go = new GameObject(typeof(T).Name);
            _gameObjects.Add(go);
            return go.AddComponent<T>();
        }
    }
}
