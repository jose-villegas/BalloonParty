using System;
using System.Threading;
using BalloonParty.Game.Telemetry;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    // The never-throw guard (R25) is the mitigation for the plan's only soft-lock risk (RK-5):
    // MessagePipe runs handlers synchronously on the publisher's stack with no exception isolation, so
    // an escaping sink exception at a flush boundary freezes the game. Written before
    // TelemetrySinkBase itself, per the plan's TDD instruction.
    [TestFixture]
    public class TelemetrySinkBaseTests
    {
        [Test]
        public void Write_WhenWriteCoreThrows_DoesNotRethrow()
        {
            var sink = new FakeSink { ThrowOnWrite = true };

            Assert.DoesNotThrow(() => sink.Write(BuildEnvelope()));
        }

        [Test]
        public void Write_AfterWriteCoreThrows_LatchesOffPermanently_SecondWriteIsANoOp()
        {
            var sink = new FakeSink { ThrowOnWrite = true };

            sink.Write(BuildEnvelope());
            sink.ThrowOnWrite = false; // would succeed now, but the sink must stay disabled regardless
            sink.Write(BuildEnvelope());

            Assert.AreEqual(1, sink.WriteCoreCalls, "WriteCore must not be called again once disabled");
        }

        [Test]
        public void FlushAsync_WhenFlushCoreThrows_DoesNotRethrow()
        {
            var sink = new FakeSink { ThrowOnFlush = true };

            Assert.DoesNotThrow(() => sink.FlushAsync(CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void FlushAsync_AfterFlushCoreThrows_LatchesOffPermanently_AlsoBlocksWrite()
        {
            var sink = new FakeSink { ThrowOnFlush = true };

            sink.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
            sink.ThrowOnFlush = false;
            sink.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
            sink.Write(BuildEnvelope());

            Assert.AreEqual(1, sink.FlushCoreCalls, "FlushCore must not be called again once disabled");
            Assert.AreEqual(0, sink.WriteCoreCalls, "the shared latch must also block Write once Flush has failed");
        }

        [Test]
        public void Dispose_WhenDisposeCoreThrows_DoesNotRethrow()
        {
            var sink = new FakeSink { ThrowOnDispose = true };

            Assert.DoesNotThrow(() => sink.Dispose());
        }

        [Test]
        public void Dispose_CalledTwice_OnlyInvokesDisposeCoreOnce()
        {
            var sink = new FakeSink();

            sink.Dispose();
            sink.Dispose();

            Assert.AreEqual(1, sink.DisposeCoreCalls);
        }

        [Test]
        public void Sink_ThatNeverThrows_KeepsWorkingNormally()
        {
            var sink = new FakeSink();

            sink.Write(BuildEnvelope());
            sink.Write(BuildEnvelope());
            sink.FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
            sink.Dispose();

            Assert.AreEqual(2, sink.WriteCoreCalls);
            Assert.AreEqual(1, sink.FlushCoreCalls);
            Assert.AreEqual(1, sink.DisposeCoreCalls);
        }

        private static TelemetryEnvelope BuildEnvelope()
        {
            var scope = MetricScope.Create(MetricScopeKind.Level, 1, () => 0f);
            var metrics = scope.Seal(1, true);
            return new TelemetryEnvelope(RecordKind.Level, 1, "session", 1, 1, 0, 1, 1, true, false, "LevelUp", 0L, metrics);
        }

        // The Dispose/_disposed split exists so a prior write failure cannot leak the resource
        // DisposeCore releases — Dispose checks _disposed, deliberately not _disabled.
        [Test]
        public void Dispose_AfterWriteCoreThrew_StillInvokesDisposeCore()
        {
            var sink = new FakeSink { ThrowOnWrite = true };

            sink.Write(BuildEnvelope());
            sink.Dispose();

            Assert.AreEqual(1, sink.DisposeCoreCalls, "a disabled sink must still release its resource");
        }

        // JsonLinesTelemetrySink is registered both AsSelf and inside CompositeTelemetrySink as the
        // same singleton, so VContainer disposes it twice. Idempotency is load-bearing, not defensive.
        [Test]
        public void Dispose_CalledTwice_InvokesDisposeCoreOnce()
        {
            var sink = new FakeSink();

            sink.Dispose();
            sink.Dispose();

            Assert.AreEqual(1, sink.DisposeCoreCalls);
        }

        // The serializer reuses one StringBuilder, so a nested Write would Clear() the buffer the
        // outer call is still filling — corrupting the outer record silently rather than throwing.
        [Test]
        public void Write_ReenteredFromWriteCore_DropsTheNestedRecord_AndCompletesTheOuterOne()
        {
            var sink = new FakeSink();
            sink.ReenterOnWrite = true;

            sink.Write(BuildEnvelope());

            Assert.AreEqual(1, sink.WriteCoreCalls, "the nested Write must not reach WriteCore");
        }

        private sealed class FakeSink : TelemetrySinkBase
        {
            internal int WriteCoreCalls;
            internal int FlushCoreCalls;
            internal int DisposeCoreCalls;
            internal bool ThrowOnWrite;
            internal bool ThrowOnFlush;
            internal bool ThrowOnDispose;
            internal bool ReenterOnWrite;

            protected override void WriteCore(in TelemetryEnvelope envelope)
            {
                WriteCoreCalls++;

                if (ReenterOnWrite)
                {
                    ReenterOnWrite = false;
                    Write(envelope);
                }

                if (ThrowOnWrite)
                {
                    throw new InvalidOperationException("boom");
                }
            }

            protected override UniTask FlushCoreAsync(CancellationToken cancellationToken)
            {
                FlushCoreCalls++;
                if (ThrowOnFlush)
                {
                    throw new InvalidOperationException("boom");
                }

                return UniTask.CompletedTask;
            }

            protected override void DisposeCore()
            {
                DisposeCoreCalls++;
                if (ThrowOnDispose)
                {
                    throw new InvalidOperationException("boom");
                }
            }
        }
    }
}
