using BalloonParty.Audio.Routing;
using NUnit.Framework;

namespace BalloonParty.Tests.Audio
{
    // SemitoneOffsetForStreak is the pure pitch-ramp step ItemSightSoundRouter's replay relies on —
    // factored out so it's edit-mode testable without any SlotGrid/PredictionTraceProvider/ItemSightTracker
    // setup. Chain-equality (ChainMatches) now lives on, and is tested against, ItemSightTracker instead —
    // see ItemSightTrackerTests.
    [TestFixture]
    public class ItemSightSoundRouterTests
    {
        [Test]
        public void SemitoneOffsetForStreak_StepsAWholeTonePerEntry()
        {
            Assert.AreEqual(0, ItemSightSoundRouter.SemitoneOffsetForStreak(0));
            Assert.AreEqual(2, ItemSightSoundRouter.SemitoneOffsetForStreak(1));
            Assert.AreEqual(4, ItemSightSoundRouter.SemitoneOffsetForStreak(2));
        }

        [Test]
        public void SemitoneOffsetForStreak_LongChain_ClampsAtTheCeiling()
        {
            var uncapped = ItemSightSoundRouter.SemitoneOffsetForStreak(50);

            Assert.AreEqual(24, uncapped, "capped at two octaves regardless of how long the chain is");
        }
    }
}
