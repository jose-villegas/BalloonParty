using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using NUnit.Framework;
using UnityEditor;

namespace BalloonParty.Tests.Configuration
{
    /// <summary>
    ///     Pins the Sharpshooter sky light's trail in the shipped settings: a source with no profile of its own
    ///     silently takes the field's default, so a dropped entry would leave a faint, wrong-coloured trail.
    /// </summary>
    [TestFixture]
    public class ShippedSnipeSkyLightProfilesTests
    {
        private const string DisturbancePath = "Assets/Configuration/DisturbanceFieldSettings.asset";
        private const string SmokePath = "Assets/Configuration/SmokeFieldSettings.asset";

        [Test]
        public void Disturbance_HasItsOwnWake()
        {
            IDisturbanceFieldSettings settings = AssetDatabase.LoadAssetAtPath<DisturbanceFieldSettings>(DisturbancePath);
            Assert.IsNotNull(settings, $"{DisturbancePath} should load");

            var profile = settings.GetProfile(StampSource.SnipeSkyLight);

            Assert.IsTrue((profile.Sources & StampSource.SnipeSkyLight) != 0, "SnipeSkyLight should have a profile");
            Assert.GreaterOrEqual(profile.Strength, 4f,
                "the wake and the landing burst should shove the clouds visibly (1.5 read as too weak, 2026-11-15)");
        }

        [Test]
        public void Smoke_IsTheWhiteSkyLightEntry()
        {
            ISmokeFieldSettings settings = AssetDatabase.LoadAssetAtPath<SmokeFieldSettings>(SmokePath);
            Assert.IsNotNull(settings, $"{SmokePath} should load");

            var profile = settings.GetProfile(PaintSource.SnipeSkyLight);

            Assert.IsTrue((profile.Sources & PaintSource.SnipeSkyLight) != 0, "SnipeSkyLight should have a profile");
            Assert.AreEqual(PaintColorMode.Palette, profile.ColorMode);
            Assert.AreEqual(GamePalette.SnipeSkyLightColorId, profile.PaletteColorName);
            Assert.Greater(profile.Opacity, 0f);
        }
    }
}
