using BalloonParty.Audio;
using NUnit.Framework;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class SoundHandleTests
    {
        [Test]
        public void Equals_SameVoiceIdAndGeneration_ReturnsTrue()
        {
            var a = new SoundHandle(3, 7u);
            var b = new SoundHandle(3, 7u);

            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void Equals_DifferentVoiceId_ReturnsFalse()
        {
            var a = new SoundHandle(3, 7u);
            var b = new SoundHandle(4, 7u);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_DifferentGeneration_ReturnsFalse()
        {
            var a = new SoundHandle(3, 7u);
            var b = new SoundHandle(3, 8u);

            Assert.IsFalse(a.Equals(b));
        }

        [Test]
        public void Equals_ObjectOverload_NonSoundHandle_ReturnsFalse()
        {
            var handle = new SoundHandle(3, 7u);

            Assert.IsFalse(handle.Equals("not a handle"));
        }

        [Test]
        public void IsValid_ZeroGeneration_ReturnsFalse()
        {
            var handle = new SoundHandle(3, 0u);

            Assert.IsFalse(handle.IsValid);
        }

        [Test]
        public void IsValid_NonZeroGeneration_ReturnsTrue()
        {
            var handle = new SoundHandle(3, 1u);

            Assert.IsTrue(handle.IsValid);
        }

        [Test]
        public void None_IsInvalid()
        {
            Assert.IsFalse(SoundHandle.None.IsValid);
        }

        [Test]
        public void None_EqualsDefault()
        {
            Assert.AreEqual(default(SoundHandle), SoundHandle.None);
        }

        [Test]
        public void OperatorEquals_SameValues_ReturnsTrue()
        {
            var a = new SoundHandle(5, 2u);
            var b = new SoundHandle(5, 2u);

            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
        }

        [Test]
        public void OperatorNotEquals_DifferentGeneration_ReturnsTrue()
        {
            var a = new SoundHandle(5, 2u);
            var b = new SoundHandle(5, 3u);

            Assert.IsTrue(a != b);
            Assert.IsFalse(a == b);
        }

        [Test]
        public void GetHashCode_EqualHandles_ReturnSameHash()
        {
            var a = new SoundHandle(9, 4u);
            var b = new SoundHandle(9, 4u);

            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }
    }
}
