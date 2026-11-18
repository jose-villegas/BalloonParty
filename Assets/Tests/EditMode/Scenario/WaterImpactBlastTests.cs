using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Scenario;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Tests.Scenario
{
    /// <summary>A blast's way to the water: on a stamp's impact report, through the bus, into the collector.</summary>
    [TestFixture]
    public class WaterImpactBlastTests
    {
        private ImpactEventBus _bus;
        private DisturbanceFieldService _field;

        [SetUp]
        public void SetUp()
        {
            _bus = new ImpactEventBus();
            _field = new DisturbanceFieldService(
                Substitute.For<IDisturbanceFieldSettings>(), Substitute.For<IGameDisplayConfiguration>(), _bus);
        }

        [Test]
        public void AnImpactReportedWithoutABlast_CarriesNone()
        {
            _bus.Report(Vector3.zero, 0.5f, Vector2.up);
            _field.Stamp(new Vector3(1f, 2f, 0f), 0.4f, 0.8f, Vector2.zero);

            Assert.AreEqual(2, _bus.Pending.Count);
            Assert.IsFalse(_bus.Pending[0].Blast.IsBlast);
            Assert.IsFalse(_bus.Pending[1].Blast.IsBlast);
            Assert.AreEqual(0.4f, _bus.Pending[1].Radius, 1e-6f);
        }

        [Test]
        public void AStampsBlast_RidesOnItsImpact_BesideTheStampsOwnRadius()
        {
            _field.Stamp(
                new Vector3(1f, 2f, 0f), 12f, 9f, Vector2.zero, blast: new ImpactEventBus.Blast(3f, 2f, 0.4f));

            Assert.AreEqual(1, _bus.Pending.Count);
            var impact = _bus.Pending[0];
            Assert.AreEqual(12f, impact.Radius, 1e-6f, "the impact everything else reads is the stamp's");
            Assert.IsTrue(impact.Blast.IsBlast);
            Assert.AreEqual(3f, impact.Blast.Radius, 1e-6f);
            Assert.AreEqual(2f, impact.Blast.Strength, 1e-6f);
            Assert.AreEqual(0.4f, impact.Blast.Seconds, 1e-6f);
        }

        [Test]
        public void ABlastOfNoStrengthOrNoRadius_IsNoBlast()
        {
            Assert.IsFalse(new ImpactEventBus.Blast(1.25f, 0f, 0.1f).IsBlast);
            Assert.IsFalse(new ImpactEventBus.Blast(0f, 1f, 0.1f).IsBlast);
            Assert.IsTrue(new ImpactEventBus.Blast(1.25f, 1f, 0f).IsBlast);
        }

        [Test]
        public void AStampThatReportsNoImpact_ReportsNoBlastEither()
        {
            var blast = new ImpactEventBus.Blast(3f, 2f, 0.4f);

            _field.Stamp(Vector3.zero, 1f, -1f, Vector2.zero, blast: blast);
            _field.Stamp(Vector3.zero, 1f, 1f, Vector2.zero, reportImpact: false, blast: blast);

            Assert.AreEqual(0, _bus.Pending.Count);
        }

        [Test]
        public void ABlastReportedByItself_IsAnImpactOfNoReach()
        {
            _field.ReportBlast(new Vector3(1f, 2f, 0f), new ImpactEventBus.Blast(0.45f, 1.3f, 0.4f));
            _field.ReportBlast(new Vector3(1f, 2f, 0f), new ImpactEventBus.Blast(0.45f, 0f, 0.4f));

            Assert.AreEqual(1, _bus.Pending.Count, "a blast of no strength is not reported at all");
            Assert.AreEqual(0f, _bus.Pending[0].Radius);
            Assert.IsTrue(_bus.Pending[0].Blast.IsBlast);
        }

        [Test]
        public void TheCollector_KeepsABlast_EvenPastItsFill()
        {
            var collector = new WaterImpactCollector(_bus);
            for (var i = 0; i < 80; i++)
            {
                _bus.Report(new Vector3(i, 0f, 0f), 0.3f);
            }

            _bus.Report(new Vector3(-5f, 0f, 0f), 3.5f, default, new ImpactEventBus.Blast(1.25f, 1f, 0.1f));
            ((IPostTickable)collector).PostTick();

            var blasts = 0;
            foreach (var impact in collector.Impacts)
            {
                if (impact.Blast.IsBlast)
                {
                    blasts++;
                    Assert.AreEqual(-5f, impact.Position.x, 1e-6f);
                }
            }

            Assert.AreEqual(1, blasts);
            Assert.AreEqual(64, collector.Impacts.Count);
        }
    }
}
