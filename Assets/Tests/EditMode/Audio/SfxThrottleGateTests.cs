using BalloonParty.Audio;
using NUnit.Framework;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class SfxThrottleGateTests
    {
        private float _now;

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
        }

        [Test]
        public void TryPass_FirstCall_Passes()
        {
            var gate = new SfxThrottleGate(() => _now, coalesceWindowSeconds: 1f, maxBurstPerWindow: 3);

            var passed = gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0.1f, out var burstIndex);

            Assert.IsTrue(passed);
            Assert.AreEqual(0, burstIndex);
        }

        [Test]
        public void TryPass_WithinCooldown_IsBlocked()
        {
            var gate = new SfxThrottleGate(() => _now, coalesceWindowSeconds: 1f, maxBurstPerWindow: 10);
            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0.1f, out _);

            _now = 0.05f;
            var passed = gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0.1f, out var burstIndex);

            Assert.IsFalse(passed);
            Assert.AreEqual(0, burstIndex);
        }

        [Test]
        public void TryPass_AfterCooldownElapses_Passes()
        {
            var gate = new SfxThrottleGate(() => _now, coalesceWindowSeconds: 1f, maxBurstPerWindow: 10);
            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0.1f, out _);

            _now = 0.2f;
            var passed = gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0.1f, out _);

            Assert.IsTrue(passed);
        }

        [Test]
        public void TryPass_WithinWindow_BurstIndexIncrements()
        {
            var gate = new SfxThrottleGate(() => _now, coalesceWindowSeconds: 1f, maxBurstPerWindow: 10);

            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0f, out var burst0);
            _now = 0.1f;
            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0f, out var burst1);
            _now = 0.2f;
            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0f, out var burst2);

            Assert.AreEqual(0, burst0);
            Assert.AreEqual(1, burst1);
            Assert.AreEqual(2, burst2);
        }

        [Test]
        public void TryPass_BurstCapReached_OverflowIsDropped()
        {
            var gate = new SfxThrottleGate(() => _now, coalesceWindowSeconds: 1f, maxBurstPerWindow: 2);

            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0f, out _);
            _now = 0.1f;
            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0f, out _);

            _now = 0.2f;
            var passed = gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0f, out var burstIndex);

            Assert.IsFalse(passed);
            Assert.AreEqual(0, burstIndex);
        }

        [Test]
        public void TryPass_AfterWindowElapses_BurstIndexResets()
        {
            var gate = new SfxThrottleGate(() => _now, coalesceWindowSeconds: 1f, maxBurstPerWindow: 2);

            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0f, out _);
            _now = 0.1f;
            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0f, out _);

            _now = 1.5f;
            var passed = gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 0f, out var burstIndex);

            Assert.IsTrue(passed);
            Assert.AreEqual(0, burstIndex);
        }

        [Test]
        public void Reset_ClearsCooldownAndBurstState()
        {
            var gate = new SfxThrottleGate(() => _now, coalesceWindowSeconds: 1f, maxBurstPerWindow: 1);
            gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 10f, out _);

            gate.Reset();
            _now = 0.01f;
            var passed = gate.TryPass(GameSoundId.BalloonPop, cooldownSeconds: 10f, out var burstIndex);

            Assert.IsTrue(passed);
            Assert.AreEqual(0, burstIndex);
        }
    }
}
