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

        [Test]
        public void FormatOrRaw_SeveralArguments_SubstitutesEach()
        {
            Assert.AreEqual("Level 3: Quick Hands", "Level {0}: {1}".FormatOrRaw(3, "Quick Hands"));
        }

        [Test]
        public void FormatOrRaw_AFormatThatSkipsAnArgument_Substitutes()
        {
            Assert.AreEqual("Level 3", "Level {0}".FormatOrRaw(3, "Quick Hands"));
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
