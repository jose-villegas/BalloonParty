using BalloonParty.Audio;
using NUnit.Framework;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class VoiceLimiterTests
    {
        private VoiceLimiter _limiter;

        [SetUp]
        public void SetUp()
        {
            _limiter = new VoiceLimiter(globalCap: 4);
        }

        [Test]
        public void TryAcquire_UnderCaps_ReturnsDistinctSlotIds()
        {
            var acquired1 = _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 4, priority: 10, out var voice1, out var stolen1);
            var acquired2 = _limiter.TryAcquire(GameSoundId.ShotFired, perIdCap: 4, priority: 10, out var voice2, out var stolen2);

            Assert.IsTrue(acquired1);
            Assert.IsTrue(acquired2);
            Assert.AreNotEqual(voice1, voice2);
            Assert.AreEqual(-1, stolen1);
            Assert.AreEqual(-1, stolen2);
            Assert.AreEqual(2, _limiter.ActiveCount);
        }

        [Test]
        public void TryAcquire_PerIdCapReached_StealsOldestSameId()
        {
            _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 2, priority: 10, out var first, out _);
            _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 2, priority: 10, out _, out _);

            var acquired = _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 2, priority: 10, out var voiceId, out var stolenVoiceId);

            Assert.IsTrue(acquired);
            Assert.AreEqual(first, voiceId);
            Assert.AreEqual(voiceId, stolenVoiceId);
            Assert.AreEqual(2, _limiter.ActiveCount);
            Assert.AreEqual(2, _limiter.ActiveCountFor(GameSoundId.BalloonPop));
        }

        [Test]
        public void TryAcquire_GlobalCapReached_EqualPriorityStealsLowestPrioritySlot()
        {
            _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 4, priority: 10, out _, out _);
            _limiter.TryAcquire(GameSoundId.ShotFired, perIdCap: 4, priority: 5, out var slotB, out _);
            _limiter.TryAcquire(GameSoundId.ItemBomb, perIdCap: 4, priority: 10, out _, out _);
            _limiter.TryAcquire(GameSoundId.ItemLaser, perIdCap: 4, priority: 10, out _, out _);

            // globalCap = 4 and all slots are full; ShotFired (priority 5) is the lowest, and an
            // equal-priority request must still be able to steal it (>=, not >).
            var acquired = _limiter.TryAcquire(GameSoundId.LevelUp, perIdCap: 4, priority: 5, out var voiceId, out var stolenVoiceId);

            Assert.IsTrue(acquired);
            Assert.AreEqual(slotB, voiceId);
            Assert.AreEqual(slotB, stolenVoiceId);
            Assert.AreEqual(0, _limiter.ActiveCountFor(GameSoundId.ShotFired));
            Assert.AreEqual(1, _limiter.ActiveCountFor(GameSoundId.LevelUp));
            Assert.AreEqual(4, _limiter.ActiveCount);
        }

        [Test]
        public void TryAcquire_GlobalCapReached_StrictlyLowerPriorityIsDropped()
        {
            // This is the pop-can't-starve-a-stinger guarantee: a low-priority request against a
            // full board of higher-priority voices must be dropped, not steal one.
            _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 4, priority: 10, out _, out _);
            _limiter.TryAcquire(GameSoundId.ShotFired, perIdCap: 4, priority: 10, out _, out _);
            _limiter.TryAcquire(GameSoundId.ItemBomb, perIdCap: 4, priority: 10, out _, out _);
            _limiter.TryAcquire(GameSoundId.ItemLaser, perIdCap: 4, priority: 10, out _, out _);

            var acquired = _limiter.TryAcquire(GameSoundId.LevelUp, perIdCap: 4, priority: 1, out var voiceId, out var stolenVoiceId);

            Assert.IsFalse(acquired);
            Assert.AreEqual(-1, voiceId);
            Assert.AreEqual(-1, stolenVoiceId);
            Assert.AreEqual(4, _limiter.ActiveCount);
            Assert.AreEqual(0, _limiter.ActiveCountFor(GameSoundId.LevelUp));
        }

        [Test]
        public void Release_FreesSlotAndDecrementsCounts()
        {
            _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 4, priority: 10, out var voiceId, out _);

            _limiter.Release(voiceId);

            Assert.AreEqual(0, _limiter.ActiveCount);
            Assert.AreEqual(0, _limiter.ActiveCountFor(GameSoundId.BalloonPop));
        }

        [Test]
        public void Release_FreedSlotIsReusedByNextAcquire()
        {
            _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 4, priority: 10, out var voiceId, out _);
            _limiter.Release(voiceId);

            _limiter.TryAcquire(GameSoundId.ShotFired, perIdCap: 4, priority: 10, out var nextVoiceId, out _);

            Assert.AreEqual(voiceId, nextVoiceId);
        }

        [Test]
        public void Release_InvalidVoiceId_IsNoOp()
        {
            _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 4, priority: 10, out _, out _);

            _limiter.Release(-1);
            _limiter.Release(99);

            Assert.AreEqual(1, _limiter.ActiveCount);
        }

        [Test]
        public void Clear_RestoresFullCapacity()
        {
            _limiter.TryAcquire(GameSoundId.BalloonPop, perIdCap: 4, priority: 10, out _, out _);
            _limiter.TryAcquire(GameSoundId.ShotFired, perIdCap: 4, priority: 10, out _, out _);

            _limiter.Clear();

            Assert.AreEqual(0, _limiter.ActiveCount);
            Assert.AreEqual(0, _limiter.ActiveCountFor(GameSoundId.BalloonPop));

            for (var i = 0; i < 4; i++)
            {
                Assert.IsTrue(_limiter.TryAcquire(GameSoundId.ItemBomb, perIdCap: 4, priority: 10, out _, out _));
            }

            Assert.AreEqual(4, _limiter.ActiveCount);
        }
    }
}
