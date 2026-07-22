using System;
using BalloonParty.EditorUI.Utilities;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.EditorUI.Tests.Utilities
{
    [TestFixture]
    public class StyleCacheTests
    {
        [SetUp]
        public void SetUp()
        {
            StyleCache.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            StyleCache.Clear();
        }

        [Test]
        public void Get_SameKey_ReturnsSameInstance()
        {
            var first = StyleCache.Get("shared", () => new GUIStyle());
            var second = StyleCache.Get("shared", () => new GUIStyle());

            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void Get_DifferentKeys_ReturnsDifferentInstances()
        {
            var first = StyleCache.Get("first", () => new GUIStyle());
            var second = StyleCache.Get("second", () => new GUIStyle());

            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void Get_SameKey_CallsFactoryOnlyOnce()
        {
            var callCount = 0;

            StyleCache.Get("shared", () =>
            {
                callCount++;
                return new GUIStyle();
            });

            StyleCache.Get("shared", () =>
            {
                callCount++;
                return new GUIStyle();
            });

            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Get_NullKey_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => StyleCache.Get(null, () => new GUIStyle()));

            Assert.That(exception?.ParamName, Is.EqualTo("key"));
        }

        [Test]
        public void Get_NullFactory_ThrowsArgumentNullException()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => StyleCache.Get("key", null));

            Assert.That(exception?.ParamName, Is.EqualTo("factory"));
        }

        [Test]
        public void Clear_AfterCachedValue_AllowsFactoryToCreateFreshInstance()
        {
            var first = StyleCache.Get("shared", () => new GUIStyle());

            StyleCache.Clear();

            var second = StyleCache.Get("shared", () => new GUIStyle());

            Assert.That(second, Is.Not.SameAs(first));
        }
    }
}
