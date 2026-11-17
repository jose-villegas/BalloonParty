using System.Collections.Generic;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    ///     An effects map renders only while <see cref="LayerPresence" /> says its layer has something alive. If it
    ///     says empty while an effect is out, that effect draws outside the glass; if it never says empty, the map
    ///     saves nothing.
    /// </summary>
    [TestFixture]
    public class LayerPresenceTests
    {
        private const int EffectsLayer = 17;
        private const int TrailLayer = 7;
        private const int Effects = 1 << EffectsLayer;
        private const int Trail = 1 << TrailLayer;

        private readonly List<Object> _made = new();

        [SetUp]
        public void SetUp()
        {
            Assume.That(LayerPresence.Any(Effects | Trail), Is.False, "something else left an effect alive");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var made in _made)
            {
                if (made != null)
                {
                    Object.DestroyImmediate(made);
                }
            }

            _made.Clear();
        }

        [Test]
        public void Track_IsPresentOnItsRenderersLayer_WhileActive()
        {
            var effect = Effect("Pop", EffectsLayer);

            LayerPresence.Track(effect);

            Assert.IsTrue(LayerPresence.Any(Effects));
            Assert.IsFalse(LayerPresence.Any(Trail), "only on its own layer");

            effect.SetActive(false);
            Assert.IsFalse(LayerPresence.Any(Effects));

            effect.SetActive(true);
            Assert.IsTrue(LayerPresence.Any(Effects));
        }

        [Test]
        public void Track_TakesAnInactiveChildsLayer_NotTheRoots()
        {
            var root = Made(new GameObject("Splash"));
            var blob = Effect("Blob", EffectsLayer);
            blob.transform.SetParent(root.transform);
            blob.SetActive(false);

            LayerPresence.Track(root);

            Assert.IsTrue(LayerPresence.Any(Effects), "a hidden blob of a splash still out counts");
        }

        [Test]
        public void Track_WithoutRenderers_NeverCounts()
        {
            var empty = Made(new GameObject("Empty") { layer = EffectsLayer });

            LayerPresence.Track(empty);

            Assert.IsFalse(LayerPresence.Any(Effects));
            Assert.IsFalse(empty.TryGetComponent<LayerPresenceTag>(out _));
        }

        [Test]
        public void Track_Twice_CountsOnce()
        {
            var effect = Effect("Pop", EffectsLayer);

            LayerPresence.Track(effect);
            LayerPresence.Track(effect);
            effect.SetActive(false);

            Assert.IsFalse(LayerPresence.Any(Effects));
        }

        [Test]
        public void Any_HoldsUntilTheLastOneLeaves()
        {
            var first = Effect("First", EffectsLayer);
            var second = Effect("Second", EffectsLayer);
            LayerPresence.Track(first);
            LayerPresence.Track(second);

            first.SetActive(false);
            Assert.IsTrue(LayerPresence.Any(Effects));

            Object.DestroyImmediate(second);
            Assert.IsFalse(LayerPresence.Any(Effects));
        }

        [Test]
        public void Any_SeesEitherLayerOfAMask()
        {
            LayerPresence.Track(Effect("Trail", TrailLayer));

            Assert.IsTrue(LayerPresence.Any(Effects | Trail));
        }

        [Test]
        public void Watch_IsPresentOnlyWhileItsRendererIsShown_UntilDestroyed()
        {
            var cloud = Effect("Cloud", EffectsLayer);
            var renderer = cloud.GetComponent<Renderer>();
            renderer.enabled = false;

            LayerPresence.Watch(renderer);
            Assert.IsFalse(LayerPresence.Any(Effects));

            renderer.enabled = true;
            Assert.IsTrue(LayerPresence.Any(Effects));

            Object.DestroyImmediate(cloud);
            Assert.IsFalse(LayerPresence.Any(Effects));
        }

        [Test]
        public void APooledEffect_CountsFromGetToReturn()
        {
            var prefab = Made(new GameObject("PopPrefab") { layer = EffectsLayer });
            prefab.AddComponent<ParticleSystem>();
            prefab.SetActive(false);
            var channel = new ParticlePoolChannel(prefab);

            var pop = channel.Get();
            _made.Add(pop.gameObject);
            Assert.IsTrue(LayerPresence.Any(Effects));

            channel.Return(pop);
            Assert.IsFalse(LayerPresence.Any(Effects));
        }

        private GameObject Effect(string name, int layer)
        {
            var go = Made(new GameObject(name) { layer = layer });
            go.AddComponent<SpriteRenderer>();
            return go;
        }

        private GameObject Made(GameObject go)
        {
            _made.Add(go);
            return go;
        }
    }
}
