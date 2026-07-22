using System;
using System.Reflection;
using BalloonParty.EditorUI.Palette;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.EditorUI.Tests.Utilities
{
    [TestFixture]
    public class EditorAssetCacheTests
    {
        private TestAsset _firstAsset;
        private TestAsset _secondAsset;

        [SetUp]
        public void SetUp()
        {
            _firstAsset = ScriptableObject.CreateInstance<TestAsset>();
            _secondAsset = ScriptableObject.CreateInstance<TestAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_firstAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(_firstAsset);
            }

            if (_secondAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(_secondAsset);
            }
        }

        [Test]
        public void Value_BeforeAccess_DoesNotCallFinder()
        {
            var callCount = 0;
            CreateCache(() =>
            {
                callCount++;
                return new[] { _firstAsset };
            });

            Assert.That(callCount, Is.Zero);
        }

        [Test]
        public void Value_CacheHit_UsesFinderOnlyOnce()
        {
            var callCount = 0;
            var cache = CreateCache(() =>
            {
                callCount++;
                return new[] { _firstAsset };
            });

            var first = cache.Value;
            var second = cache.Value;

            Assert.That(first, Is.SameAs(_firstAsset));
            Assert.That(second, Is.SameAs(_firstAsset));
            Assert.That(callCount, Is.EqualTo(1));
        }

        [Test]
        public void Invalidate_AfterCachedValue_SearchesAgain()
        {
            var callCount = 0;
            var cache = CreateCache(() =>
            {
                callCount++;
                return callCount == 1 ? new[] { _firstAsset } : new[] { _secondAsset };
            });

            Assert.That(cache.Value, Is.SameAs(_firstAsset));

            cache.Invalidate();

            Assert.That(cache.Value, Is.SameAs(_secondAsset));
            Assert.That(callCount, Is.EqualTo(2));
        }

        [Test]
        public void Value_EmptyResults_ReturnsNull()
        {
            var cache = CreateCache(Array.Empty<TestAsset>);

            Assert.That(cache.Value, Is.Null);
        }

        [Test]
        public void Constructor_NullFinder_ThrowsArgumentNullException()
        {
            var constructor = GetConstructor();

            var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(new object[] { null }));

            Assert.That(exception?.InnerException, Is.TypeOf<ArgumentNullException>());
            Assert.That(((ArgumentNullException)exception.InnerException).ParamName, Is.EqualTo("finder"));
        }

        private static EditorAssetCache<TestAsset> CreateCache(Func<TestAsset[]> finder)
        {
            return (EditorAssetCache<TestAsset>)GetConstructor().Invoke(new object[] { finder });
        }

        private static ConstructorInfo GetConstructor()
        {
            var constructor = typeof(EditorAssetCache<TestAsset>).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Func<TestAsset[]>) },
                null);

            Assert.That(constructor, Is.Not.Null);
            return constructor;
        }

        private sealed class TestAsset : ScriptableObject
        {
        }
    }
}
