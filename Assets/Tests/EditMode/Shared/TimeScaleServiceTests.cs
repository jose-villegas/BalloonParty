using BalloonParty.Shared.Pause;
using NUnit.Framework;
using UnityEngine;

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
        public void ClaimAboveOne_CannotExceedNormalSpeed()
        {
            // The resolution is min(claims, 1) — a >1 claim can't push the game past normal speed.
            _service.Claim(TimeScaleSource.Cinematic, 2f);

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
    }
}
