using BalloonParty.Shared.Extensions;
using NUnit.Framework;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class StringFormatExtensionsTests
    {
        [Test]
        public void FormatOrRaw_AWellFormedFormat_Substitutes()
        {
            Assert.AreEqual("Level 4", "Level {0}".FormatOrRaw(4));
        }

        [TestCase("Level {")]
        [TestCase("Level {1}")]
        public void FormatOrRaw_AMalformedFormat_ComesBackAsWritten(string format)
        {
            Assert.AreEqual(format, format.FormatOrRaw(4), "a typo in an authored format must never throw mid-flow");
        }

        [TestCase(null)]
        [TestCase("")]
        public void FormatOrRaw_NoFormat_IsEmpty(string format)
        {
            Assert.AreEqual(string.Empty, format.FormatOrRaw(4));
        }
    }
}
