using BalloonParty.Shared.Extensions;
using NUnit.Framework;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class MusicalPitchExtensionsTests
    {
        private const float Delta = 0.0001f;

        [Test]
        public void SemitonesToPitchMultiplier_Zero_ReturnsOne()
        {
            Assert.AreEqual(1f, (0).SemitonesToPitchMultiplier(), Delta);
        }

        [Test]
        public void SemitonesToPitchMultiplier_PlusOctave_ReturnsDouble()
        {
            Assert.AreEqual(2f, (12).SemitonesToPitchMultiplier(), Delta);
        }

        [Test]
        public void SemitonesToPitchMultiplier_MinusOctave_ReturnsHalf()
        {
            Assert.AreEqual(0.5f, (-12).SemitonesToPitchMultiplier(), Delta);
        }

        [Test]
        public void SemitonesToPitchMultiplier_IntOverload_MatchesFloatOverload()
        {
            const int semitones = 5;

            Assert.AreEqual(((float)semitones).SemitonesToPitchMultiplier(), semitones.SemitonesToPitchMultiplier(), Delta);
        }
    }
}
