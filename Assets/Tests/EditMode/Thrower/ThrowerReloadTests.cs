using BalloonParty.Game.Level;
using BalloonParty.Shared;
using BalloonParty.Thrower;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Thrower
{
    /// <summary>
    ///     The thrower's reload rules: a release fires only once the held ball has slid out, with a slot
    ///     free under the cap, during play; and the slide itself is the cue, so it must not read "ready"
    ///     before a release would actually fire.
    /// </summary>
    [TestFixture]
    public class ThrowerReloadTests
    {
        private const int Cap = 3;
        private const float Cooldown = 0.5f;

        private IProjectileFlightConfig _config;
        private ThrowerReload _reload;

        [SetUp]
        public void SetUp()
        {
            _config = Substitute.For<IProjectileFlightConfig>();
            _config.MaxBallsInFlight.Returns(Cap);
            _config.ReloadCooldownSeconds.Returns(Cooldown);
            _config.ProjectileLoadDuration.Returns(0.2f);

            _reload = new ThrowerReload(_config);
            _reload.Restart();
        }

        [Test]
        public void IsReady_SlidOutFreeSlotPlaying_True()
        {
            _reload.Advance(Cooldown, 0);

            Assert.IsTrue(_reload.IsReady(0, LevelUpPhase.Playing));
        }

        [Test]
        public void IsReady_BallStillSlidingOut_False()
        {
            _reload.Advance(Cooldown * 0.4f, 0);

            Assert.IsFalse(_reload.IsReady(0, LevelUpPhase.Playing),
                "a release before the ball reaches the muzzle must not fire");
        }

        [Test]
        public void IsReady_CapReached_False()
        {
            _reload.Advance(Cooldown, 0);

            Assert.IsFalse(_reload.IsReady(Cap, LevelUpPhase.Playing));
        }

        [Test]
        public void IsReady_OneSlotBelowTheCap_True()
        {
            _reload.Advance(Cooldown, Cap - 1);

            Assert.IsTrue(_reload.IsReady(Cap - 1, LevelUpPhase.Playing));
        }

        [Test]
        public void IsReady_NotPlaying_False()
        {
            _reload.Advance(Cooldown, 0);

            Assert.IsFalse(_reload.IsReady(0, LevelUpPhase.Completing), "no shot may leave during the level-up ceremony");
        }

        [Test]
        public void Restart_TheCooldownFloorsAShorterSlide()
        {
            _reload.Advance(0.3f, 0);

            Assert.IsFalse(_reload.IsLoaded, "the 0.2 s slide must still wait out the 0.5 s cooldown");
        }

        [Test]
        public void Restart_ASlideLongerThanTheCooldownSetsTheReload()
        {
            _config.ProjectileLoadDuration.Returns(0.8f);
            _reload.Restart();

            _reload.Advance(Cooldown, 0);

            Assert.IsFalse(_reload.IsLoaded);
        }

        [Test]
        public void Advance_AtTheCap_HoldsTheBallBack()
        {
            _reload.Advance(Cooldown * 2f, Cap);

            Assert.AreEqual(0f, _reload.Progress, "with every slot taken the ball must stay at the pivot");
        }

        [Test]
        public void Advance_ASlotFrees_TheSlideResumesWhereItStopped()
        {
            _reload.Advance(Cooldown * 0.5f, 0);
            _reload.Advance(Cooldown, Cap);
            _reload.Advance(Cooldown * 0.5f, Cap - 1);

            Assert.IsTrue(_reload.IsLoaded);
        }

        [Test]
        public void SlidePosition_NeverReachesTheMuzzleBeforeTheBallIsReady()
        {
            // An overshooting ease reached the muzzle at about a third of the reload and sat there while
            // releases were dropped; the cue must not say ready before the rule does.
            const int steps = 50;
            for (var i = 0; i < steps - 1; i++)
            {
                _reload.Advance(Cooldown / steps, 0);

                Assert.IsFalse(_reload.IsLoaded);
                Assert.Less(_reload.SlidePosition, 1f, $"at step {i + 1} of {steps}");
            }

            _reload.Advance(Cooldown, 0);
            Assert.AreEqual(1f, _reload.SlidePosition);
        }

        [Test]
        public void CanLaunch_SkipsTheReloadButNotTheCapOrThePhase()
        {
            Assert.IsTrue(_reload.CanLaunch(0, LevelUpPhase.Playing), "tools snap the ball out, skipping the slide");
            Assert.IsFalse(_reload.CanLaunch(Cap, LevelUpPhase.Playing));
            Assert.IsFalse(_reload.CanLaunch(0, LevelUpPhase.Completing));
        }
    }
}
