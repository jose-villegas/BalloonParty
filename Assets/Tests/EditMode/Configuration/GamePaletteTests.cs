using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class GamePaletteTests
    {
        private GamePalette _palette;

        [SetUp]
        public void SetUp()
        {
            _palette = ScriptableObject.CreateInstance<GamePalette>();
            SetField(_palette, "_colors", new[] { CreateEntry("Red"), CreateEntry("Blue"), CreateEntry("Green") });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_palette);
        }

        [Test]
        public void ColorNamesForMask_AllBitsSet_ReturnsEveryColor()
        {
            CollectionAssert.AreEqual(new[] { "Red", "Blue", "Green" }, _palette.ColorNamesForMask(~0));
        }

        [Test]
        public void ColorNamesForMask_NoBitsSet_ReturnsEmpty()
        {
            Assert.IsEmpty(_palette.ColorNamesForMask(0));
        }

        [Test]
        public void ColorNamesForMask_SingleBit_ReturnsMatchingColorByIndex()
        {
            CollectionAssert.AreEqual(new[] { "Blue" }, _palette.ColorNamesForMask(1 << 1));
        }

        private static PaletteEntry CreateEntry(string name)
        {
            var entry = new PaletteEntry();
            SetField(entry, "_name", name);
            return entry;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}
