using BalloonParty.Configuration.Effects;
using NUnit.Framework;
using UnityEditor;

namespace BalloonParty.Tests.Configuration
{
    /// <summary>
    ///     Pins the Paint item's ink in the shipped settings: a source with no profile of its own takes the field's
    ///     default, which isn't paper-only, so a dropped entry would leave smoke on every level, not just the page.
    /// </summary>
    [TestFixture]
    public class ShippedPaintSplashProfileTests
    {
        private const string SmokePath = "Assets/Configuration/SmokeFieldSettings.asset";

        [Test]
        public void PaintSplash_StampsOnlyOnPaper_InTheHoldersColour()
        {
            ISmokeFieldSettings settings = AssetDatabase.LoadAssetAtPath<SmokeFieldSettings>(SmokePath);
            Assert.IsNotNull(settings, $"{SmokePath} should load");

            var profile = settings.GetProfile(PaintSource.PaintSplash);

            Assert.IsTrue((profile.Sources & PaintSource.PaintSplash) != 0, "PaintSplash should have a profile");
            Assert.IsTrue(profile.PaperOnly, "the paint item's ink belongs on the page only");
            Assert.AreEqual(PaintColorMode.Dynamic, profile.ColorMode);
            Assert.Greater(profile.Opacity, 0f);
        }

        [Test]
        public void TheOtherSources_StampEverywhere()
        {
            ISmokeFieldSettings settings = AssetDatabase.LoadAssetAtPath<SmokeFieldSettings>(SmokePath);

            foreach (var source in new[]
                     {
                         PaintSource.ProjectileTrail, PaintSource.ToughPop, PaintSource.ToughBreathing,
                         PaintSource.ToughDeflect, PaintSource.SnipeSkyLight
                     })
            {
                Assert.IsFalse(settings.GetProfile(source).PaperOnly, $"{source} should still smoke on every level");
            }
        }
    }
}
