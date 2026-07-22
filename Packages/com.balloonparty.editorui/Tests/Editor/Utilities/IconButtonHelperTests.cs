using BalloonParty.EditorUI.Utilities;
using NUnit.Framework;

namespace BalloonParty.EditorUI.Tests.Utilities
{
    [TestFixture]
    public class IconButtonHelperTests
    {
        [SetUp]
        public void SetUp()
        {
            IconButtonHelper.ClearCache();
        }

        [TearDown]
        public void TearDown()
        {
            IconButtonHelper.ClearCache();
        }

        [Test]
        public void Get_MissingIcon_UsesFallbackGlyph()
        {
            var content = IconButtonHelper.Get("missing-icon-for-tests", "?", "tip");

            Assert.That(content.image, Is.Null);
            Assert.That(content.text, Is.EqualTo("?"));
            Assert.That(content.tooltip, Is.EqualTo("tip"));
        }

        [Test]
        public void Get_SameIconName_ReturnsCachedContent()
        {
            var first = IconButtonHelper.Get("missing-icon-for-cache-tests", "A", "first");
            var second = IconButtonHelper.Get("missing-icon-for-cache-tests", "B", "second");

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.text, Is.EqualTo("A"));
            Assert.That(second.tooltip, Is.EqualTo("first"));
        }

        [Test]
        public void ClearCache_NextGetReturnsNewInstance()
        {
            var first = IconButtonHelper.Get("missing-icon-for-clear-tests", "A");

            IconButtonHelper.ClearCache();

            var second = IconButtonHelper.Get("missing-icon-for-clear-tests", "A");

            Assert.That(second, Is.Not.SameAs(first));
        }
    }
}
