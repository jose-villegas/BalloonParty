using BalloonParty.UI.Notices;
using NUnit.Framework;

namespace BalloonParty.Tests.UI
{
    [TestFixture]
    public class WriteOnMathTests
    {
        [Test]
        public void CornerPosition_TheRightEdge_IsOneCharacterOn()
        {
            Assert.AreEqual(3f, WriteOnMath.CornerPosition(2, right: true, top: false, slant: 0.4f));
        }

        [Test]
        public void CornerPosition_ATopCorner_LeadsItsBottomBySlant()
        {
            var bottom = WriteOnMath.CornerPosition(2, right: false, top: false, slant: 0.4f);
            var top = WriteOnMath.CornerPosition(2, right: false, top: true, slant: 0.4f);

            Assert.AreEqual(0.4f, bottom - top, 1e-5f, "the top is revealed first, so the edge leans forward");
        }

        [Test]
        public void RevealAlpha_BeforeTheHeadArrives_IsHidden()
        {
            Assert.AreEqual(0f, WriteOnMath.RevealAlpha(head: 2f, position: 3f, softness: 0.8f));
        }

        [Test]
        public void RevealAlpha_InsideTheSoftEdge_IsPartial()
        {
            Assert.AreEqual(0.5f, WriteOnMath.RevealAlpha(head: 3.4f, position: 3f, softness: 0.8f), 1e-5f);
        }

        [Test]
        public void RevealAlpha_PastTheSoftEdge_IsFullyShown()
        {
            Assert.AreEqual(1f, WriteOnMath.RevealAlpha(head: 5f, position: 3f, softness: 0.8f));
        }

        [Test]
        public void RevealAlpha_NoSoftness_IsAHardCut()
        {
            Assert.AreEqual(0f, WriteOnMath.RevealAlpha(head: 2.99f, position: 3f, softness: 0f));
            Assert.AreEqual(1f, WriteOnMath.RevealAlpha(head: 3f, position: 3f, softness: 0f));
        }

        [Test]
        public void EndHead_RevealsTheLastCornerFully()
        {
            const int count = 5;
            const float softness = 0.8f;
            var lastCorner = WriteOnMath.CornerPosition(count - 1, right: true, top: false, slant: 0.3f);

            Assert.AreEqual(1f, WriteOnMath.RevealAlpha(WriteOnMath.EndHead(count, softness), lastCorner, softness));
        }

        [Test]
        public void StartHead_ADelay_ReachesTheFirstCharacterAfterIt()
        {
            const float secondsPerCharacter = 0.07f;
            var head = WriteOnMath.StartHead(0.3f, secondsPerCharacter);

            Assert.AreEqual(0f, head + 0.3f / secondsPerCharacter, 1e-4f);
        }

        [Test]
        public void StartHead_DuringTheDelay_EveryCornerIsHidden()
        {
            var head = WriteOnMath.StartHead(0.3f, 0.07f);
            var firstTop = WriteOnMath.CornerPosition(0, right: false, top: true, slant: 0.35f);

            Assert.AreEqual(0f, WriteOnMath.RevealAlpha(head, firstTop, 0.8f), "a slanted top corner must not show early");
        }

        [Test]
        public void StartHead_NoDelay_IsZero()
        {
            Assert.AreEqual(0f, WriteOnMath.StartHead(0f, 0.07f));
            Assert.AreEqual(0f, WriteOnMath.StartHead(-1f, 0.07f));
        }

        [Test]
        public void EndHead_AtTheStart_EveryCornerIsHidden()
        {
            var firstBottom = WriteOnMath.CornerPosition(0, right: false, top: false, slant: 0.3f);

            Assert.AreEqual(0f, WriteOnMath.RevealAlpha(0f, firstBottom, 0.8f));
        }
    }
}
