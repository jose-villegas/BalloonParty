using System.Globalization;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Telemetry;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    // String-shape assertions only — there is no external consumer yet to pin an exact schema against,
    // so these check the properties R27 actually requires: catalog-driven wire names, zero-skipping,
    // and culture safety.
    [TestFixture]
    public class TelemetryEnvelopeSerializerTests
    {
        private IGamePalette _palette;
        private TelemetryEnvelopeSerializer _serializer;

        [SetUp]
        public void SetUp()
        {
            _palette = Substitute.For<IGamePalette>();
            _palette.ProgressColorNames.Returns(new[] { "Red", "Blue" });
            _serializer = new TelemetryEnvelopeSerializer(_palette);
        }

        [Test]
        public void Serialize_IncludesEveryScalarEnvelopeField()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 1, () => 0f);
            var envelope = new TelemetryEnvelope(
                RecordKind.Level, 3, "abc-session", 5, 2, 1, 7, 2, true, true, "GameOver", 0L, scope.Seal(7, true));

            var json = _serializer.Serialize(envelope);

            Assert.That(json, Does.Contain("\"record_kind\":\"level\""));
            Assert.That(json, Does.Contain("\"schema_version\":3"));
            Assert.That(json, Does.Contain("\"session_id\":\"abc-session\""));
            Assert.That(json, Does.Contain("\"run_id\":5"));
            Assert.That(json, Does.Contain("\"attempt_id\":2"));
            Assert.That(json, Does.Contain("\"attempt_index\":1"));
            Assert.That(json, Does.Contain("\"level_index\":7"));
            Assert.That(json, Does.Contain("\"level_attempt_ordinal\":2"));
            Assert.That(json, Does.Contain("\"completed\":true"));
            Assert.That(json, Does.Contain("\"cheat_active\":true"));
            Assert.That(json, Does.Contain("\"end_cause\":\"GameOver\""));
        }

        [Test]
        public void Serialize_EmitsEveryRecordKind_UnderItsWireName()
        {
            var scope = MetricScope.Create(MetricScopeKind.Run, 1, () => 0f);

            Assert.That(_serializer.Serialize(BuildEnvelope(RecordKind.Flight, scope)), Does.Contain("\"record_kind\":\"flight\""));
            Assert.That(_serializer.Serialize(BuildEnvelope(RecordKind.Run, scope)), Does.Contain("\"record_kind\":\"run\""));
            Assert.That(_serializer.Serialize(BuildEnvelope(RecordKind.Session, scope)), Does.Contain("\"record_kind\":\"session\""));
        }

        [Test]
        public void Serialize_SkipsZeroValuedCounters_ButEmitsNonzeroUnderItsWireName()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 1, () => 0f);
            scope.Add(MetricId.ShotsFired, 4);

            var json = _serializer.Serialize(BuildLevel(scope));

            Assert.That(json, Does.Contain("\"shots_fired\":4"));
            Assert.That(json, Does.Not.Contain("\"flights_started\""));
        }

        [Test]
        public void Serialize_AlwaysEmitsAllFourTimers_EvenAtZero()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 1, () => 0f);

            var json = _serializer.Serialize(BuildLevel(scope));

            Assert.That(json, Does.Contain("\"gameplay_seconds\":0"));
            Assert.That(json, Does.Contain("\"ceremony_seconds\":0"));
            Assert.That(json, Does.Contain("\"wall_seconds\":0"));
            Assert.That(json, Does.Contain("\"hold_seconds\":0"));
        }

        [Test]
        public void Serialize_EmitsAxisBreakdown_ForANonzeroBucket_KeyedByWireNameAndAxis()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 2, () => 0f);
            scope.IncrementAxis(MetricId.Pops, MetricAxis.Color, 1);

            var json = _serializer.Serialize(BuildLevel(scope));

            Assert.That(json, Does.Contain("\"pops_color\":{\"Blue\":1}"));
        }

        // Buckets are named, never indexed. An index-keyed history silently re-labels itself the next
        // time an enum gains a member — BalloonType already gained Tougher once — and nothing
        // downstream could detect it.
        [Test]
        public void Serialize_KeysAxisBucketsByName_NotByDenseIndex()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 3, () => 0f);
            scope.IncrementAxis(MetricId.Pops, MetricAxis.BalloonType, (int)BalloonType.Tougher);
            scope.IncrementAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)ItemType.Bomb);
            scope.IncrementAxis(MetricId.Pops, MetricAxis.Color, 0);

            var json = _serializer.Serialize(BuildLevel(scope));

            Assert.That(json, Does.Contain("\"pops_balloon_type\":{\"Tougher\":1}"));
            Assert.That(json, Does.Contain("\"items_activated_item_type\":{\"Bomb\":1}"));
            Assert.That(json, Does.Contain("\"pops_color\":{\"Red\":1}"));
        }

        // The colour axis carries one bucket past the palette for ids that are not progress colours:
        // rainbow's sentinel, paint-converted, or an actor with no colour at all.
        [Test]
        public void Serialize_NamesTheTrailingColorBucket_Other()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 3, () => 0f);
            scope.IncrementAxis(MetricId.Pops, MetricAxis.Color, 2);

            var json = _serializer.Serialize(BuildLevel(scope));

            Assert.That(json, Does.Contain("\"pops_color\":{\"other\":1}"));
        }

        [Test]
        public void Serialize_OmitsAnAxisSlot_WhenEveryBucketIsZero()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 1, () => 0f);

            var json = _serializer.Serialize(BuildLevel(scope));

            Assert.That(json, Does.Not.Contain("\"pops_color\""));
        }

        // The one call site this feature adds beyond "the two InvariantCulture call sites in the repo
        // today" (per the plan) — a comma-decimal current culture must never leak into the wire format.
        [Test]
        public void Serialize_UsesInvariantCulture_RegardlessOfCurrentCulture()
        {
            var original = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            try
            {
                var now = 0f;
                var scope = MetricScope.Create(MetricScopeKind.Level, 1, () => now);
                scope.Timer(TimerId.Gameplay).Resume();
                now = 12.5f;

                var json = _serializer.Serialize(BuildLevel(scope));

                Assert.That(json, Does.Contain("\"gameplay_seconds\":12.5"));
                Assert.That(json, Does.Not.Contain("12,5"));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Test]
        public void Serialize_ReusesTheSameBuilder_AcrossCalls()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 1, () => 0f);

            var first = _serializer.Serialize(BuildLevel(scope));
            var second = _serializer.Serialize(BuildLevel(scope));

            Assert.AreEqual(first, second);
        }

        private static TelemetryEnvelope BuildLevel(MetricScope scope)
        {
            return new TelemetryEnvelope(
                RecordKind.Level, 1, "session", 1, 1, 0, 1, 1, true, false, "LevelUp", 0L, scope.Seal(1, true));
        }

        private static TelemetryEnvelope BuildEnvelope(RecordKind kind, MetricScope scope)
        {
            return new TelemetryEnvelope(
                kind, 1, "session", 1, 1, 0, 1, 1, true, false, "LevelUp", 0L, scope.Seal());
        }
    }
}
