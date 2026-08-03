using System;
using System.Threading;
using BalloonParty.Game.Telemetry;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class CompositeTelemetrySinkTests
    {
        [Test]
        public void Write_WithEmptyArray_IsANoOp()
        {
            var composite = new CompositeTelemetrySink(Array.Empty<ITelemetrySink>());

            Assert.DoesNotThrow(() => composite.Write(BuildEnvelope()));
        }

        [Test]
        public void Write_FansOutToEveryLeaf()
        {
            var first = new RecordingTelemetrySink();
            var second = new RecordingTelemetrySink();
            var composite = new CompositeTelemetrySink(new ITelemetrySink[] { first, second });

            composite.Write(BuildEnvelope());

            Assert.AreEqual(1, first.Written.Count);
            Assert.AreEqual(1, second.Written.Count);
        }

        [Test]
        public void FlushAsync_WithEmptyArray_CompletesWithoutThrowing()
        {
            var composite = new CompositeTelemetrySink(Array.Empty<ITelemetrySink>());

            Assert.DoesNotThrow(() => composite.FlushAsync(CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void FlushAsync_ReachesEveryLeaf()
        {
            var first = new RecordingTelemetrySink();
            var second = new RecordingTelemetrySink();
            var composite = new CompositeTelemetrySink(new ITelemetrySink[] { first, second });

            composite.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, first.FlushCalls);
            Assert.AreEqual(1, second.FlushCalls);
        }

        [Test]
        public void Dispose_ReachesEveryLeaf()
        {
            var first = new RecordingTelemetrySink();
            var second = new RecordingTelemetrySink();
            var composite = new CompositeTelemetrySink(new ITelemetrySink[] { first, second });

            composite.Dispose();

            Assert.AreEqual(1, first.DisposeCalls);
            Assert.AreEqual(1, second.DisposeCalls);
        }

        private static TelemetryEnvelope BuildEnvelope()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 1, () => 0f);
            var metrics = scope.Seal(1, true);
            return new TelemetryEnvelope(RecordKind.Level, 1, "session", 1, 1, 0, 1, 1, true, false, "LevelUp", 0L, metrics);
        }
    }
}
