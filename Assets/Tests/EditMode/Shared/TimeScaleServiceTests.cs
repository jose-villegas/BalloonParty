using BalloonParty.Shared.Pause;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class TimeScaleServiceTests
    {
        private TimeScaleService _service;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
            _service = new TimeScaleService();
        }

        [TearDown]
        public void TearDown()
        {
            // Never leak a warped editor clock into other tests.
            Time.timeScale = 1f;
        }

        [Test]
        public void Claim_AppliesTheClaimedValue()
        {
            _service.Claim(TimeScaleSource.Cinematic, 0.3f);

            Assert.AreEqual(0.3f, Time.timeScale, 0.001f);
        }

        [Test]
        public void LowestActiveClaimWins()
        {
            _service.Claim(TimeScaleSource.Cinematic, 0.3f);
            _service.Claim(TimeScaleSource.LevelUpPopup, 0f);

            Assert.AreEqual(0f, Time.timeScale, 0.001f);
        }

        [Test]
        public void Release_FallsBackToTheNextClaim()
        {
            _service.Claim(TimeScaleSource.Cinematic, 0.3f);
            _service.Claim(TimeScaleSource.LevelUpPopup, 0f);

            _service.Release(TimeScaleSource.LevelUpPopup);

            Assert.AreEqual(0.3f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ReleasingTheLastClaim_RestoresNormalSpeed()
        {
            _service.Claim(TimeScaleSource.Cinematic, 0.3f);

            _service.Release(TimeScaleSource.Cinematic);

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ReclaimingSameSource_ReplacesItsValue()
        {
            _service.Claim(TimeScaleSource.Cinematic, 0.3f);
            _service.Claim(TimeScaleSource.Cinematic, 0.8f);

            Assert.AreEqual(0.8f, Time.timeScale, 0.001f);
        }

        [Test]
        public void SingleClaimAboveOne_AllowsSpeedUp()
        {
            // After the change: a single non-exclusive claim > 1 is allowed (enables post-cinematic ramp).
            _service.Claim(TimeScaleSource.LevelUpCeremony, 2f);

            Assert.AreEqual(2f, Time.timeScale, 0.001f);
        }

        [Test]
        public void MultipleClaimsAboveOne_MinimumWins()
        {
            _service.Claim(TimeScaleSource.LevelUpCeremony, 2f);
            _service.Claim(TimeScaleSource.Cinematic, 1.5f);

            Assert.AreEqual(1.5f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ClaimAboveOne_WithSubOneClaim_SubOneWins()
        {
            // A slow-down claim must still beat a speed-up — minimum semantics.
            _service.Claim(TimeScaleSource.LevelUpCeremony, 2f);
            _service.Claim(TimeScaleSource.LastShield, 0.5f);

            Assert.AreEqual(0.5f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ExclusiveClaimAboveOne_StillCappedAtOne()
        {
            // Exclusive path retains the cap at 1 — only non-exclusive allows > 1.
            _service.ClaimExclusive(TimeScaleSource.LevelUpCeremony, 1.5f);

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        [Test]
        public void NegativeClaim_ClampsToZero()
        {
            _service.Claim(TimeScaleSource.Cinematic, -0.5f);

            Assert.AreEqual(0f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ReleaseWithoutClaim_IsANoOp()
        {
            _service.Claim(TimeScaleSource.Cinematic, 0.3f);

            _service.Release(TimeScaleSource.LevelUpPopup);

            Assert.AreEqual(0.3f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ResetRun_ClearsAllClaims()
        {
            _service.Claim(TimeScaleSource.Cinematic, 0.3f);
            _service.Claim(TimeScaleSource.LevelUpPopup, 0f);

            _service.ResetRun(1);

            Assert.AreEqual(1f, Time.timeScale, 0.001f);
        }

        // --- Exclusivity tests ---

        [Test]
        public void ExclusiveClaim_IgnoresLowerCompetingClaim()
        {
            _service.Claim(TimeScaleSource.LastShield, 0.25f);
            _service.ClaimExclusive(TimeScaleSource.LevelUpCeremony, 0.6f);

            Assert.AreEqual(0.6f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ReleaseExclusive_RestoresMinimumOfStillRecordedClaims()
        {
            _service.Claim(TimeScaleSource.LastShield, 0.25f);
            _service.ClaimExclusive(TimeScaleSource.LevelUpCeremony, 0.6f);

            _service.ReleaseExclusive(TimeScaleSource.LevelUpCeremony);

            Assert.AreEqual(0.25f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ClaimArrivingDuringExclusivity_IsRecordedAndAppliesAfterRelease()
        {
            _service.ClaimExclusive(TimeScaleSource.LevelUpCeremony, 0.6f);
            _service.Claim(TimeScaleSource.PierceDischarge, 0.1f);

            // During exclusivity, the exclusive value wins.
            Assert.AreEqual(0.6f, Time.timeScale, 0.001f);

            _service.ReleaseExclusive(TimeScaleSource.LevelUpCeremony);

            // After release, the recorded claim applies.
            Assert.AreEqual(0.1f, Time.timeScale, 0.001f);
        }

        [Test]
        public void Release_OnExclusiveOwner_ClearsOwnerToo()
        {
            _service.Claim(TimeScaleSource.LastShield, 0.25f);
            _service.ClaimExclusive(TimeScaleSource.LevelUpCeremony, 0.6f);

            // Using plain Release on the exclusive owner should clear both the claim and the owner.
            _service.Release(TimeScaleSource.LevelUpCeremony);

            Assert.AreEqual(0.25f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ResetRun_ClearsExclusiveOwner_PlainClaimAppliesNormally()
        {
            _service.ClaimExclusive(TimeScaleSource.LevelUpCeremony, 0.6f);

            _service.ResetRun(1);

            // After reset, a plain claim should apply normally (no stale exclusive owner).
            _service.Claim(TimeScaleSource.Cinematic, 0.4f);

            Assert.AreEqual(0.4f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ReleaseExclusive_ByNonOwner_IsANoOp()
        {
            _service.Claim(TimeScaleSource.LastShield, 0.25f);
            _service.ClaimExclusive(TimeScaleSource.LevelUpCeremony, 0.6f);

            // A non-owner trying to release exclusivity should be ignored.
            _service.ReleaseExclusive(TimeScaleSource.LastShield);

            Assert.AreEqual(0.6f, Time.timeScale, 0.001f);
        }

        [Test]
        public void ClaimExclusive_OverridesExistingExclusiveOwner()
        {
            _service.ClaimExclusive(TimeScaleSource.LevelUpCeremony, 0.6f);
            LogAssert.Expect(LogType.Assert,
                "<color=#C3E88D>[TimeScale]</color> Two different sources competing for exclusivity: LevelUpCeremony vs Cinematic");
            _service.ClaimExclusive(TimeScaleSource.Cinematic, 0.5f);

            // Latest exclusive owner wins.
            Assert.AreEqual(0.5f, Time.timeScale, 0.001f);

            _service.ReleaseExclusive(TimeScaleSource.Cinematic);

            // The previous owner's claim is still in the dict — it participates in min().
            Assert.AreEqual(0.6f, Time.timeScale, 0.001f);
        }
    }
}
