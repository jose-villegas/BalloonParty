using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Projectile.Controller;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     What a piercing shot (a Snipe lance, a cruise pierce) tells the impact bus as it flies, for the water: two
    ///     blasts along every stretch of its flight, from where it was armed to the wall its charge ends at, and there
    ///     one small round blast on the wall stamp every shot leaves. An ordinary ball reports neither.
    /// </summary>
    [TestFixture]
    public class PierceWakeTests
    {
        private const float StepSeconds = 0.02f;

        private static readonly Vector3 From = new(0f, -4f, 0f);
        private static readonly Vector3 To = new(0.5f, -2f, 0f);
        private static readonly Vector3 Wall = new(1f, 4.5f, 0f);
        private static readonly Vector3 Past = new(1.2f, 4f, 0f);

        private ImpactEventBus _bus;
        private DisturbanceFieldService _field;
        private IProjectileFlightConfig _config;

        [SetUp]
        public void SetUp()
        {
            _bus = new ImpactEventBus();
            var fieldSettings = Substitute.For<IDisturbanceFieldSettings>();
            // No ramp: a ramped stamp needs the field's scheduler, which a substitute's settings give no room.
            fieldSettings.GetProfile(StampSource.ProjectileImpact)
                .Returns(new StampProfile { Sources = StampSource.ProjectileImpact, Radius = 0.3f, Strength = 7f });
            _field = new DisturbanceFieldService(fieldSettings, Substitute.For<IGameDisplayConfiguration>(), _bus);

            _config = Substitute.For<IProjectileFlightConfig>();
            _config.PierceWakeWaveRadius.Returns(1.75f);
            _config.PierceWakeWaveStrength.Returns(0.17f);
            _config.PierceWakeHeartRadius.Returns(0.35f);
            _config.PierceWakeHeartStrength.Returns(0.53f);
            _config.PierceWallImpactRadius.Returns(0.75f);
            _config.PierceWallImpactStrength.Returns(0.35f);
            _config.PierceWallImpactSeconds.Returns(0.3f);
        }

        [Test]
        public void ALanceInFlight_ReportsAWaveAndAHeart_AlongTheStretchItFlew()
        {
            Report(ProjectileStep.Moved(To, Vector2.up, 20f), wasPiercing: true, stillPiercing: true);

            Assert.AreEqual(2, _bus.Pending.Count);
            var wave = _bus.Pending[0];
            var heart = _bus.Pending[1];
            foreach (var impact in new[] { wave, heart })
            {
                Assert.AreEqual(0f, impact.Radius, "an impact of no reach: only what answers a blast answers it");
                Assert.That(Vector3.Distance((From + To) * 0.5f, impact.Position), Is.LessThan(1e-5f));
                Assert.That(Vector2.Distance(To - From, impact.Blast.Line), Is.LessThan(1e-5f));
                Assert.AreEqual(StepSeconds, impact.Blast.Seconds, 1e-6f);
            }

            Assert.AreEqual(1.75f, wave.Blast.Radius, 1e-6f);
            Assert.AreEqual(0.17f, wave.Blast.Strength, 1e-6f);
            Assert.AreEqual(0.35f, heart.Blast.Radius, 1e-6f);
            Assert.AreEqual(0.53f, heart.Blast.Strength, 1e-6f);
        }

        [Test]
        public void AnOrdinaryBall_LeavesNoWake_FlyingOrAtAWall()
        {
            Report(ProjectileStep.Moved(To, Vector2.up, 20f), wasPiercing: false, stillPiercing: false);
            Report(ProjectileStep.Bounced(Past, Wall, Vector2.down, 20f), wasPiercing: false, stillPiercing: false);
            Report(ProjectileStep.Destroyed(Wall, Vector2.up, 20f), wasPiercing: false, stillPiercing: false);

            Assert.AreEqual(0, _bus.Pending.Count);
        }

        // The wake belongs to the flight: a shot armed in mid air starts one from there, on the step it is armed in.
        [Test]
        public void AShotArmedThisStep_StartsItsWake()
        {
            Report(ProjectileStep.Moved(To, Vector2.up, 20f), wasPiercing: false, stillPiercing: true);

            Assert.AreEqual(2, _bus.Pending.Count);
        }

        [Test]
        public void ALanceThatBouncesStillArmed_TurnsItsWakeAtTheWall()
        {
            Report(ProjectileStep.Bounced(Past, Wall, Vector2.down, 20f), wasPiercing: true, stillPiercing: true);

            Assert.AreEqual(4, _bus.Pending.Count);
            Assert.That(Vector2.Distance(Wall - From, _bus.Pending[0].Blast.Line), Is.LessThan(1e-5f));
            Assert.That(Vector2.Distance(Past - Wall, _bus.Pending[2].Blast.Line), Is.LessThan(1e-5f));
        }

        [Test]
        public void ALanceWhoseChargeEndsAtTheWall_LeavesItsWakeUpToTheWall_AndNoneBeyond()
        {
            Report(ProjectileStep.Bounced(Past, Wall, Vector2.down, 20f), wasPiercing: true, stillPiercing: false);

            Assert.AreEqual(2, _bus.Pending.Count);
            Assert.That(Vector2.Distance(Wall - From, _bus.Pending[0].Blast.Line), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance((From + Wall) * 0.5f, _bus.Pending[1].Position), Is.LessThan(1e-5f));
        }

        [Test]
        public void ALanceThatDiesAtAWall_LeavesItsWakeUpToIt()
        {
            Report(ProjectileStep.Destroyed(Wall, Vector2.up, 20f), wasPiercing: true, stillPiercing: false);

            Assert.AreEqual(2, _bus.Pending.Count);
            Assert.That(Vector2.Distance(Wall - From, _bus.Pending[0].Blast.Line), Is.LessThan(1e-5f));
        }

        // A stalled lance must not dig a hole: no travel, no report.
        [Test]
        public void ALanceThatDidNotMove_ReportsNothing()
        {
            Report(ProjectileStep.Moved(From, Vector2.up, 0f), wasPiercing: true, stillPiercing: true);

            Assert.AreEqual(0, _bus.Pending.Count);
        }

        [Test]
        public void WallBlast_IsTheSmallCrater_WhereALancesChargeEnds()
        {
            var plowed = PierceWake.WallBlast(_config, ProjectileStepOutcome.Bounced, true, false, 3);
            var plowedWithAChargeBanked = PierceWake.WallBlast(_config, ProjectileStepOutcome.Bounced, true, true, 2);
            var died = PierceWake.WallBlast(_config, ProjectileStepOutcome.Destroyed, true, false, 0);

            foreach (var blast in new[] { plowed, plowedWithAChargeBanked, died })
            {
                Assert.IsTrue(blast.IsBlast);
                Assert.AreEqual(0.75f, blast.Radius, 1e-6f);
                Assert.AreEqual(0.35f, blast.Strength, 1e-6f);
                Assert.AreEqual(0.3f, blast.Seconds, 1e-6f);
                Assert.AreEqual(Vector2.zero, blast.Line);
            }
        }

        [Test]
        public void WallBlast_IsNone_ForAnOrdinaryBall_AndForALanceThatOnlyBounces()
        {
            Assert.IsFalse(PierceWake.WallBlast(_config, ProjectileStepOutcome.Bounced, false, false, 0).IsBlast);
            Assert.IsFalse(PierceWake.WallBlast(_config, ProjectileStepOutcome.Destroyed, false, false, 0).IsBlast);
            Assert.IsFalse(PierceWake.WallBlast(_config, ProjectileStepOutcome.Bounced, true, true, 0).IsBlast,
                "still armed with nothing plowed: the wake turns with it");
            Assert.IsFalse(PierceWake.WallBlast(_config, ProjectileStepOutcome.Moved, true, true, 2).IsBlast);
        }

        // The wall hit goes through the stamp every shot already leaves at a wall: one impact, of the stamp's own
        // reach for everything else on the bus, carrying the blast for the water.
        [Test]
        public void TheWallStamp_CarriesTheBlast_OnItsOneImpact()
        {
            _field.StampShieldLoss(
                StampSource.ProjectileImpact, Wall, 0.5f,
                blast: PierceWake.WallBlast(_config, ProjectileStepOutcome.Bounced, true, false, 1));
            _field.StampShieldLoss(StampSource.ProjectileImpact, Wall, 0.5f);

            Assert.AreEqual(2, _bus.Pending.Count);
            Assert.AreEqual(0.3f, _bus.Pending[0].Radius, 1e-6f);
            Assert.AreEqual(0.75f, _bus.Pending[0].Blast.Radius, 1e-6f);
            Assert.AreEqual(0.3f, _bus.Pending[1].Radius, 1e-6f);
            Assert.IsFalse(_bus.Pending[1].Blast.IsBlast, "an ordinary ball's wall stamp is what it always was");
        }

        private void Report(in ProjectileStep step, bool wasPiercing, bool stillPiercing)
        {
            PierceWake.Report(_field, _config, From, step, wasPiercing, stillPiercing, StepSeconds);
        }
    }
}
