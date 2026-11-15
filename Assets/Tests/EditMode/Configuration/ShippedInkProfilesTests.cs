using BalloonParty.Configuration.Effects;
using NUnit.Framework;
using UnityEditor;

namespace BalloonParty.Tests.Configuration
{
    /// <summary>
    ///     Pins the paper's ink sources in the shipped settings: a source with no profile of its own takes the field's
    ///     default, which is smoke, so a dropped entry would leave smoke on every level instead of ink on the page.
    /// </summary>
    [TestFixture]
    public class ShippedInkProfilesTests
    {
        private const string SmokePath = "Assets/Configuration/SmokeFieldSettings.asset";

        [TestCase((int)PaintSource.PaintSplash)]
        [TestCase((int)PaintSource.BalloonPop)]
        public void InkSources_InkInTheirOwnColour_AndSmear(int sourceBits)
        {
            var source = (PaintSource)sourceBits;
            ISmokeFieldSettings settings = AssetDatabase.LoadAssetAtPath<SmokeFieldSettings>(SmokePath);
            Assert.IsNotNull(settings, $"{SmokePath} should load");

            var profile = settings.GetProfile(source);

            Assert.IsTrue((profile.Sources & source) != 0, $"{source} should have a profile");
            Assert.AreEqual(PaintLayer.Ink, profile.Layer, "only pops and paint ink the page");
            Assert.AreEqual(PaintColorMode.Dynamic, profile.ColorMode);
            Assert.Greater(profile.Opacity, 0f);
            Assert.Greater(profile.Smear, 0f);
        }

        [Test]
        public void TheOtherSources_StaySmoke()
        {
            ISmokeFieldSettings settings = AssetDatabase.LoadAssetAtPath<SmokeFieldSettings>(SmokePath);

            foreach (var source in new[]
                     {
                         PaintSource.ProjectileTrail, PaintSource.ToughPop, PaintSource.ToughBreathing,
                         PaintSource.ToughDeflect, PaintSource.SnipeSkyLight
                     })
            {
                Assert.AreEqual(PaintLayer.Smoke, settings.GetProfile(source).Layer,
                    $"{source} should still smoke, on paper as everywhere");
            }
        }
    }
}
