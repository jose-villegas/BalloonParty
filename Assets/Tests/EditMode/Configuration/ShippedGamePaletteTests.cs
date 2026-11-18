using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    /// <summary>
    ///     Pins the palette slots the scene light field is asked for by loading the real <c>GamePalette.asset</c>:
    ///     a light tags its colour as a slot index, and the field only holds the first
    ///     <see cref="PaletteChannelEncoding.Slots" />, so a missing entry or one past them draws in the key light.
    /// </summary>
    [TestFixture]
    public class ShippedGamePaletteTests
    {
        private const string AssetPath = "Assets/Configuration/GamePalette.asset";

        private GamePalette _palette;

        // The loaded asset is shared, so it's read only and never destroyed.
        [SetUp]
        public void SetUp()
        {
            _palette = AssetDatabase.LoadAssetAtPath<GamePalette>(AssetPath);
            Assert.IsNotNull(_palette, $"{AssetPath} should load");
        }

        [TestCase(GamePalette.PredictionColorId)]
        [TestCase(GamePalette.SnipeSkyLightColorId)]
        [TestCase(GamePalette.UnbreakableFormationColorId)]
        [TestCase(GamePalette.GoldenRaysColorId)]
        [TestCase(GamePalette.HeartRaysColorId)]
        [TestCase(GamePalette.DiscoRaysColorId)]
        [TestCase(GamePalette.NukeRaysColorId)]
        [TestCase(GamePalette.NukeArmedRaysColorId)]
        public void SceneLightSlot_IsInTheFieldsPalette(string colorId)
        {
            var index = _palette.PaletteIndexOf(colorId);

            Assert.GreaterOrEqual(index, 0, $"{colorId} should be in the palette");
            Assert.Less(index, PaletteChannelEncoding.Slots, $"{colorId} should be one of the field's slots");
        }

        [Test]
        public void SnipeSkyLight_IsWhite()
        {
            var color = _palette.Colors[_palette.PaletteIndexOf(GamePalette.SnipeSkyLightColorId)].Color;

            Assert.AreEqual(Color.white, color);
        }
    }
}
