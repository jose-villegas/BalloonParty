using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Telemetry;
using BalloonParty.UI.Telemetry;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.UI
{
    // MetricLabel._hideWhenZero gates on the isZero the resolver reports alongside the text. An earlier
    // revision string-matched the rendered output against { "0", "0:00", "0%" } instead, which meant a
    // stat line silently stopped hiding the day a metric's unit changed its shape — and, less visibly,
    // on any culture whose digit glyphs are not ASCII. These tests pin that the flag tracks the VALUE.
    //
    // MetricLabel itself is a MonoBehaviour with [RequireComponent(typeof(TMP_Text))] and cannot be
    // constructed here: BalloonParty.Tests.EditMode.asmdef does not reference Unity.TextMeshPro. The
    // logic worth testing is the resolver's, which is reachable directly.
    [TestFixture]
    public class MetricLabelIsZeroTests
    {
        private const int ColorAxisSize = 3;

        private MetricValueResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            var palette = Substitute.For<IGamePalette>();
            palette.ProgressColorNames.Returns(new[] { "Red", "Blue" });
            _resolver = new MetricValueResolver(palette);
        }

        [Test]
        public void EveryValueKind_ReportsZeroOnAnUntouchedSnapshot()
        {
            var empty = NewScope().Seal(1, true);

            AssertZero(empty, MetricBinding.ForMetric(MetricBindingSource.CeremonyLevel, MetricId.Pops));
            AssertZero(empty, MetricBinding.ForMetric(MetricBindingSource.CeremonyLevel,
                MetricId.MaxDangerLevel));
            AssertZero(empty, MetricBinding.ForTimer(MetricBindingSource.CeremonyLevel, TimerId.Gameplay));
            AssertZero(empty, MetricBinding.ForColorBucket(MetricBindingSource.CeremonyLevel,
                MetricId.Pops, "Red"));
        }

        [Test]
        public void ANonzeroValue_DoesNotReportZero()
        {
            var scope = NewScope();
            scope.Add(MetricId.Pops, 1);
            scope.AddAxis(MetricId.Pops, MetricAxis.Color, 0, 1);
            var snapshot = scope.Seal(1, true);

            _resolver.Resolve(snapshot, MetricBinding.ForMetric(MetricBindingSource.CeremonyLevel,
                MetricId.Pops), out var metricZero);
            _resolver.Resolve(snapshot, MetricBinding.ForColorBucket(MetricBindingSource.CeremonyLevel,
                MetricId.Pops, "Red"), out var bucketZero);

            Assert.IsFalse(metricZero);
            Assert.IsFalse(bucketZero);
        }

        // The flag reads the number, so a thousands separator in the text cannot confuse it — which is
        // exactly what the old string comparison got wrong in the other direction.
        [Test]
        public void ALargeValue_DoesNotReportZero()
        {
            var scope = NewScope();
            scope.Add(MetricId.PointsBanked, 10000);

            _resolver.Resolve(scope.Seal(1, true),
                MetricBinding.ForMetric(MetricBindingSource.CeremonyLevel, MetricId.PointsBanked),
                out var isZero);

            Assert.IsFalse(isZero);
        }

        // "Nothing happened" and "something broke" are opposite states to a player. Hiding the second
        // one would swallow the only signal that a binding needs re-picking.
        [Test]
        public void AnUnresolvableBinding_DoesNotReportZero()
        {
            var snapshot = NewScope().Seal(1, true);
            var binding = MetricBinding.ForOrdinalBucket(MetricBindingSource.CeremonyLevel,
                MetricId.ShotsFired, MetricAxis.Color, 0);

            var text = _resolver.Resolve(snapshot, binding, out var isZero);

            Assert.AreEqual(MetricValueResolver.Placeholder, text);
            Assert.IsFalse(isZero, "a placeholder must stay on screen even when hide-when-zero is on");
        }

        [Test]
        public void ANullSnapshot_DoesNotReportZero()
        {
            var text = _resolver.Resolve(null,
                MetricBinding.ForMetric(MetricBindingSource.Run, MetricId.Pops), out var isZero);

            Assert.AreEqual(MetricValueResolver.Placeholder, text);
            Assert.IsFalse(isZero);
        }

        private static MetricScope NewScope()
        {
            return MetricScope.Create(MetricScopeKind.Level, ColorAxisSize, () => 0f);
        }

        private void AssertZero(ISealedMetrics snapshot, MetricBinding binding)
        {
            _resolver.Resolve(snapshot, binding, out var isZero);
            Assert.IsTrue(isZero, $"{binding.Kind} should report zero on an untouched snapshot");
        }
    }
}
