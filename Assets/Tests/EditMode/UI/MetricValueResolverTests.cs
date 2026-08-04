using System;
using System.Reflection;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Telemetry;
using BalloonParty.UI.Telemetry;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BalloonParty.Tests.UI
{
    // W4's only file with real logic: a MetricBinding plus a snapshot becomes the string a label shows.
    // Everything else in the wave is wiring the editor and a playtest cover. Every [Test] is public —
    // NUnit silently SKIPS a non-public test method in this project.
    [TestFixture]
    public class MetricValueResolverTests
    {
        private const int ColorAxisSize = 3; // Red, Blue, and the trailing "other" bucket

        private MetricValueResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.ProgressColorNames.Returns(new[] { "Red", "Blue" });
            _resolver = new MetricValueResolver(palette);
        }

        [Test]
        public void ScalarMetric_RendersWithThousandsSeparators()
        {
            var scope = NewLevelScope();
            scope.Add(MetricId.PointsBanked, 12345);

            var text = _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForMetric(MetricBindingSource.CeremonyLevel, MetricId.PointsBanked));

            Assert.AreEqual(12345.ToString("N0"), text);
        }

        // The danger level is stored in hundredths because MetricSet holds ints. Rendering the raw 87
        // would read as a count of something, which is the failure the unit column exists to prevent.
        [Test]
        public void HundredthsUnitMetric_RendersAsAPercentage()
        {
            var scope = NewLevelScope();
            scope.RecordMax(MetricId.MaxDangerLevel, 87);

            var text = _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForMetric(MetricBindingSource.CeremonyLevel, MetricId.MaxDangerLevel));

            Assert.AreEqual("87%", text);
        }

        [Test]
        public void Timer_RendersAsMinutesAndSeconds_NotABareFloat()
        {
            var now = 0f;
            var scope = MetricScope.Create(MetricScopeKind.Level, ColorAxisSize, () => now);
            scope.Timer(TimerId.Gameplay).Resume();
            now = 83f;

            var text = _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForTimer(MetricBindingSource.CeremonyLevel, TimerId.Gameplay));

            Assert.AreEqual("1:23", text);
        }

        [Test]
        public void TimerPastAnHour_KeepsTheHoursSegment()
        {
            var now = 0f;
            var scope = MetricScope.Create(MetricScopeKind.Level, ColorAxisSize, () => now);
            scope.Timer(TimerId.Wall).Resume();
            now = 3725f;

            var text = _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForTimer(MetricBindingSource.CeremonyLevel, TimerId.Wall));

            Assert.AreEqual("1:02:05", text);
        }

        [Test]
        public void ColorBucket_IsFoundByName_NotByStoredIndex()
        {
            var scope = NewLevelScope();
            scope.AddAxis(MetricId.Pops, MetricAxis.Color, 1, 7); // index 1 is Blue

            var text = _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForColorBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops, "Blue"));

            Assert.AreEqual("7", text);
        }

        // The whole reason colour buckets store a name: a palette reorder must not silently repoint a
        // label at a different colour's count.
        [Test]
        public void ColorBucket_FollowsTheColorWhenThePaletteIsReordered()
        {
            var reordered = Substitute.For<IGamePalette>();
            reordered.ProgressColorNames.Returns(new[] { "Blue", "Red" });
            var resolver = new MetricValueResolver(reordered);

            var scope = NewLevelScope();
            scope.AddAxis(MetricId.Pops, MetricAxis.Color, 1, 7); // index 1 is now Red

            var text = resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForColorBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops, "Red"));

            Assert.AreEqual("7", text, "the binding names a colour, so it must track the colour");
        }

        [Test]
        public void ColorNameNoLongerInThePalette_ShowsThePlaceholderAndWarnsOnce()
        {
            var scope = NewLevelScope();
            var binding = MetricBinding.ForColorBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops,
                "Chartreuse");
            var snapshot = scope.Seal(1, true);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Chartreuse"));
            Assert.AreEqual(MetricValueResolver.Placeholder, _resolver.Resolve(snapshot, binding));

            // A prefab-serialized binding fails identically forever; warning per resolve would flood the
            // console for the life of the popup.
            Assert.AreEqual(MetricValueResolver.Placeholder, _resolver.Resolve(snapshot, binding));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BalloonTypeBucket_UsesTheEnumOrdinalDirectly()
        {
            var scope = NewLevelScope();
            scope.AddAxis(MetricId.Pops, MetricAxis.BalloonType, (int)BalloonType.Tough, 4);

            var text = _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForOrdinalBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops,
                    MetricAxis.BalloonType, (int)BalloonType.Tough));

            Assert.AreEqual("4", text);
        }

        [Test]
        public void RecordFields_ComeOffTheLevelSnapshot()
        {
            var snapshot = NewLevelScope().Seal(6, true);

            Assert.AreEqual("6", _resolver.Resolve(snapshot,
                MetricBinding.ForRecordField(MetricBindingSource.CeremonyLevel, RecordField.LevelIndex)));
            Assert.AreEqual(true.ToString(), _resolver.Resolve(snapshot,
                MetricBinding.ForRecordField(MetricBindingSource.CeremonyLevel, RecordField.Completed)));
        }

        // A run snapshot has no level identity. Authoring that combination is a mistake, but a popup is
        // the worst possible place to throw.
        [Test]
        public void RecordFieldAgainstARunSnapshot_ShowsThePlaceholderInsteadOfThrowing()
        {
            var run = MetricScope.Create(MetricScopeKind.Run, ColorAxisSize, () => 0f).Seal();

            Assert.AreEqual(MetricValueResolver.Placeholder, _resolver.Resolve(run,
                MetricBinding.ForRecordField(MetricBindingSource.Run, RecordField.LevelIndex)));
        }

        // R20: the empty snapshot is a real runtime state — an aborted level-up clears CeremonyLevel to
        // it while the popup may still be awaiting its gate — so every binding kind must survive it.
        [Test]
        public void EveryBindingKind_RendersAgainstAnEmptySnapshot()
        {
            var empty = NewLevelScope().Seal(0, false);

            Assert.AreEqual("0", _resolver.Resolve(empty,
                MetricBinding.ForMetric(MetricBindingSource.CeremonyLevel, MetricId.Pops)));
            Assert.AreEqual("0:00", _resolver.Resolve(empty,
                MetricBinding.ForTimer(MetricBindingSource.CeremonyLevel, TimerId.Gameplay)));
            Assert.AreEqual("0", _resolver.Resolve(empty,
                MetricBinding.ForColorBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops, "Red")));
            Assert.AreEqual("0", _resolver.Resolve(empty,
                MetricBinding.ForRecordField(MetricBindingSource.CeremonyLevel, RecordField.LevelIndex)));
        }

        [Test]
        public void NullSnapshot_ShowsThePlaceholder()
        {
            Assert.AreEqual(MetricValueResolver.Placeholder, _resolver.Resolve(null,
                MetricBinding.ForMetric(MetricBindingSource.Run, MetricId.Pops)));
        }

        // BalloonType wasn't the only enum-ordinal axis under test — ItemType is a distinct code path
        // through MetricCatalog.SlotOf/AxisSlotInfo (its own axis size, its own catalog row) that
        // nothing above exercised.
        [Test]
        public void ItemTypeBucket_UsesTheEnumOrdinalDirectly()
        {
            var scope = NewLevelScope();
            scope.AddAxis(MetricId.ItemsActivated, MetricAxis.ItemType, (int)ItemType.Bomb, 2);

            var text = _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForOrdinalBucket(MetricBindingSource.CeremonyLevel, MetricId.ItemsActivated,
                    MetricAxis.ItemType, (int)ItemType.Bomb));

            Assert.AreEqual("2", text);
        }

        // Only the color-name lookup's failure path was covered (ColorNameNoLongerInThePalette...). An
        // ordinal bucket authored (or corrupted) out of range hits the SAME "index < 0 || index >=
        // buckets.Count" guard from the other side and had no coverage at all.
        [Test]
        public void OrdinalBucket_PastTheEnumsRange_ShowsThePlaceholder()
        {
            var scope = NewLevelScope();

            var text = _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForOrdinalBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops,
                    MetricAxis.BalloonType, 9999));

            Assert.AreEqual(MetricValueResolver.Placeholder, text);
        }

        [Test]
        public void OrdinalBucket_Negative_ShowsThePlaceholder()
        {
            var scope = NewLevelScope();

            var text = _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForOrdinalBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops,
                    MetricAxis.BalloonType, -1));

            Assert.AreEqual(MetricValueResolver.Placeholder, text);
        }

        // DESIGN GAP, not yet a fix: MetricsSnapshotBase.AxisBucketsOf goes through MetricCatalog.SlotOf,
        // which THROWS ArgumentException for a (MetricId, MetricAxis) pair the catalog never declared —
        // it never returns null. ResolveBucket's "if (buckets == null)" guard is therefore dead code, and
        // an AxisBucket binding authored (today only reachable by hand-constructing MetricBinding, since
        // the drawer only ever offers declared pairs — but a future catalog edit that drops an axis from
        // an id leaves an old prefab binding pointed at the now-undeclared pair) against an
        // undeclared axis throws instead of showing a placeholder. That breaks the plan's own promise —
        // "a name that no longer resolves ... never throws inside a popup" — for this one path. Pinned
        // here so the throw can't silently change to something else unnoticed; flagged for the resolver
        // to catch/guard rather than left as an implicit contract.
        [Test]
        public void AxisBucket_MetricDoesNotDeclareTheRequestedAxis_ShowsThePlaceholder()
        {
            var scope = NewLevelScope();
            // ShotsFired declares NoAxes in MetricCatalog — Color is not one of its axes.
            var binding = MetricBinding.ForOrdinalBucket(MetricBindingSource.CeremonyLevel,
                MetricId.ShotsFired, MetricAxis.Color, 0);

            // Reachable from a prefab authored against an older catalog: AxisBucketsOf routes through
            // MetricCatalog.SlotOf, which THROWS on an undeclared pair rather than returning null, and a
            // popup is the worst possible place for that.
            Assert.AreEqual(MetricValueResolver.Placeholder, _resolver.Resolve(scope.Seal(1, true), binding));
        }

        // MetricValueKind's switch in Resolve() has no live path to its default branch — every declared
        // enum member is handled — but MetricBinding is [Serializable] and ships in prefabs, so a stale
        // value from a since-removed enum member is a real corruption mode, not just a hypothetical.
        // Forces it via reflection (Kind's setter is private) rather than skipping the branch untested.
        [Test]
        public void UnrecognizedValueKind_ShowsThePlaceholder()
        {
            var scope = NewLevelScope();
            var binding = WithCorruptedKind(
                MetricBinding.ForMetric(MetricBindingSource.CeremonyLevel, MetricId.Pops), (MetricValueKind)99);

            var text = _resolver.Resolve(scope.Seal(1, true), binding);

            Assert.AreEqual(MetricValueResolver.Placeholder, text);
        }

        // A palette can legitimately ship with zero progress colors mid-configuration; the color axis
        // then collapses to just the trailing "other" bucket. TryColorBucketIndexOf must still resolve
        // "other" to bucket 0 rather than an out-of-range index.
        [Test]
        public void ColorBucket_EmptyPalette_OtherIsTheOnlyBucket()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.ProgressColorNames.Returns(Array.Empty<string>());
            var resolver = new MetricValueResolver(palette);

            var scope = MetricScope.Create(MetricScopeKind.Level, 1, () => 0f);
            scope.AddAxis(MetricId.Pops, MetricAxis.Color, 0, 5);

            var text = resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForColorBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops, "other"));

            Assert.AreEqual("5", text);
        }

        // DESIGN GAP: AxisBucketNaming.TryColorBucketIndexOf checks the literal "other" sentinel BEFORE
        // searching the palette by name, so a palette that authors an actual progress color named
        // "other" cannot be reached by its own name — every binding to "other" reads the trailing
        // catch-all bucket's count instead of that color's own count. This pins the current (surprising)
        // behavior as a regression guard, not an endorsement — see the AxisBucketNaming coverage review
        // for the direct version of this case and the flagged fix.
        [Test]
        public void ColorBucket_PaletteHasAColorLiterallyNamedOther_ResolvesToTheSentinelBucketNotItsOwn()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.ProgressColorNames.Returns(new[] { "Red", "other" });
            var resolver = new MetricValueResolver(palette);

            var scope = MetricScope.Create(MetricScopeKind.Level, 3, () => 0f);
            scope.AddAxis(MetricId.Pops, MetricAxis.Color, 1, 7); // the real "other" palette color's own count
            scope.AddAxis(MetricId.Pops, MetricAxis.Color, 2, 3); // the trailing sentinel bucket's count

            var text = resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForColorBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops, "other"));

            Assert.AreEqual("3", text,
                "documents the collision: the real color's count (7) is unreachable by name today");
        }

        // The other half of "a binding outlives the catalog it was authored against": not a metric that
        // lost an axis, but an id whose enum member was deleted outright. Every read past the guard
        // indexes a dense array or a dictionary by the stored ordinal — the counters array, UnitOf's
        // ById, AxesOf's ById — so this throws rather than reading zero, inside a popup.
        [Test]
        public void MetricIdNoLongerInTheEnum_ShowsThePlaceholderInsteadOfThrowing()
        {
            var snapshot = NewLevelScope().Seal(1, true);
            var binding = WithCorruptedField("_metric",
                MetricBinding.ForMetric(MetricBindingSource.CeremonyLevel, MetricId.Pops), 9999);

            Assert.AreEqual(MetricValueResolver.Placeholder, _resolver.Resolve(snapshot, binding));
        }

        [Test]
        public void TimerIdNoLongerInTheEnum_ShowsThePlaceholderInsteadOfThrowing()
        {
            var snapshot = NewLevelScope().Seal(1, true);
            var binding = WithCorruptedField("_timer",
                MetricBinding.ForTimer(MetricBindingSource.CeremonyLevel, TimerId.Gameplay), 9999);

            Assert.AreEqual(MetricValueResolver.Placeholder, _resolver.Resolve(snapshot, binding));
        }

        // An axis bucket reaches the catalog twice — once for the axis check, once for the unit — so it
        // needs the same guard even though its own axis is intact.
        [Test]
        public void AxisBucketOnARemovedMetricId_ShowsThePlaceholderInsteadOfThrowing()
        {
            var snapshot = NewLevelScope().Seal(1, true);
            var binding = WithCorruptedField("_metric",
                MetricBinding.ForColorBucket(MetricBindingSource.CeremonyLevel, MetricId.Pops, "Red"), 9999);

            Assert.AreEqual(MetricValueResolver.Placeholder, _resolver.Resolve(snapshot, binding));
        }

        private static MetricBinding WithCorruptedField(string field, MetricBinding binding, int ordinal)
        {
            object boxed = binding;
            var info = typeof(MetricBinding).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            info!.SetValue(boxed, Enum.ToObject(info.FieldType, ordinal));
            return (MetricBinding)boxed;
        }

        private static MetricBinding WithCorruptedKind(MetricBinding binding, MetricValueKind kind)
        {
            object boxed = binding;
            typeof(MetricBinding)
                .GetField("_kind", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(boxed, kind);
            return (MetricBinding)boxed;
        }

        private static MetricScope NewLevelScope()
        {
            return MetricScope.Create(MetricScopeKind.Level, ColorAxisSize, () => 0f);
        }
    }
}
