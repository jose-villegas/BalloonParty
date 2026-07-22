using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Scenario;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Tests.Scenario
{
    [TestFixture]
    public class PaintingFieldServiceTests
    {
        private IPaintingFieldSettings _settings;
        private IGameDisplayConfiguration _display;
        private IGamePalette _palette;
        private ISubscriber<LevelUpDismissedMessage> _levelUpDismissedSubscriber;
        private PaintingFieldService _service;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<IPaintingFieldSettings>();
            _settings.DecayTickInterval.Returns(0.05f);
            _settings.DecayRate.Returns(0.08f);
            _settings.WindSpeed.Returns(0.4f);
            _settings.WindInfluence.Returns(1f);
            _settings.WindAgeBias.Returns(1.5f);
            _settings.WindDirection.Returns(new Vector2(1f, 0f));

            _display = Substitute.For<IGameDisplayConfiguration>();
            _palette = Substitute.For<IGamePalette>();

            // Set up a 3-color palette.
            var entries = new List<PaletteEntry> { CreateEntry("Red", Color.red), CreateEntry("Blue", Color.blue), CreateEntry("Green", Color.green) };
            _palette.Colors.Returns(entries);

            _levelUpDismissedSubscriber = Substitute.For<ISubscriber<LevelUpDismissedMessage>>();
            _levelUpDismissedSubscriber
                .Subscribe(Arg.Any<IMessageHandler<LevelUpDismissedMessage>>(), Arg.Any<MessageHandlerFilter<LevelUpDismissedMessage>[]>())
                .Returns(Substitute.For<System.IDisposable>());

            _service = new PaintingFieldService(_settings, _display, _palette, _levelUpDismissedSubscriber);
        }

        // --- SetWindDampen min-accumulator ---

        [Test]
        public void SetWindDampen_SingleCall_StoresClampedValue()
        {
            _service.SetWindDampen(0.5f);

            Assert.AreEqual(0.5f, GetWindDampen(), 0.001f);
        }

        [Test]
        public void SetWindDampen_MultipleCalls_TakesMinimum()
        {
            _service.SetWindDampen(0.8f);
            _service.SetWindDampen(0.3f);
            _service.SetWindDampen(0.6f);

            Assert.AreEqual(0.3f, GetWindDampen(), 0.001f);
        }

        [Test]
        public void SetWindDampen_ValueAboveOne_ClampsToOne()
        {
            _service.SetWindDampen(2.5f);

            Assert.AreEqual(1f, GetWindDampen(), 0.001f);
        }

        [Test]
        public void SetWindDampen_NegativeValue_ClampsToZero()
        {
            _service.SetWindDampen(-0.5f);

            Assert.AreEqual(0f, GetWindDampen(), 0.001f);
        }

        [Test]
        public void SetWindDampen_DefaultIsOne_BeforeAnyCalls()
        {
            Assert.AreEqual(1f, GetWindDampen(), 0.001f);
        }

        // --- Stamp rejection (resources not ready) ---

        [Test]
        public void Stamp_WhenResourcesNotReady_DoesNotQueue()
        {
            // Resources are never initialized so IsReady == false.
            _service.Stamp(Vector3.zero, 1f, 0);

            Assert.AreEqual(0, GetPendingStampCount());
        }

        [Test]
        public void Stamp_NegativePaletteIndex_DoesNotQueue()
        {
            // Even if resources were ready, invalid index should reject.
            _service.Stamp(Vector3.zero, 1f, -1);

            Assert.AreEqual(0, GetPendingStampCount());
        }

        [Test]
        public void Stamp_PaletteIndexOutOfRange_DoesNotQueue()
        {
            // Palette has 3 entries (indices 0-2); index 3 is OOB.
            _service.Stamp(Vector3.zero, 1f, 3);

            Assert.AreEqual(0, GetPendingStampCount());
        }

        // --- Tick early-exit when resources not ready ---

        [Test]
        public void Tick_WhenResourcesNotReady_DoesNotAccumulatePaintingTime()
        {
            ((ITickable)_service).Tick();

            Assert.AreEqual(0f, GetPaintingTime(), 0.001f);
        }

        // --- Helpers ---

        private float GetWindDampen()
        {
            var field = typeof(PaintingFieldService).GetField("_windDampen", BindingFlags.NonPublic | BindingFlags.Instance);
            return (float)field!.GetValue(_service);
        }

        private int GetPendingStampCount()
        {
            var field = typeof(PaintingFieldService).GetField("_pendingStamps", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<object>)null;
            var raw = field!.GetValue(_service);
            // Use IList to get count from the generic list regardless of inner type.
            return ((System.Collections.IList)raw).Count;
        }

        private float GetPaintingTime()
        {
            var field = typeof(PaintingFieldService).GetField("_paintingTime", BindingFlags.NonPublic | BindingFlags.Instance);
            return (float)field!.GetValue(_service);
        }

        private static PaletteEntry CreateEntry(string name, Color color)
        {
            var entry = new PaletteEntry();
            SetField(entry, "_name", name);
            SetField(entry, "_color", color);
            return entry;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field!.SetValue(target, value);
        }
    }
}
