using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Level;
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
    public class SmokeFieldServiceTests
    {
        private static readonly int InkActiveId = Shader.PropertyToID("_InkFieldActive");

        private ISmokeFieldSettings _settings;
        private IGameDisplayConfiguration _display;
        private IGamePalette _palette;
        private ISubscriber<LevelUpDismissedMessage> _levelUpDismissedSubscriber;
        private IMessageHandler<LevelTransitionCompletedMessage> _transitionCompleted;
        private SmokeFieldService _service;
        private ActiveScenario _scenario;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<ISmokeFieldSettings>();
            _settings.DecayTickInterval.Returns(0.05f);
            _settings.DecayRate.Returns(0.08f);
            _settings.WindSpeed.Returns(0.4f);
            _settings.WindInfluence.Returns(1f);
            _settings.WindAgeBias.Returns(1.5f);
            _settings.WindDirection.Returns(new Vector2(1f, 0f));
            _settings.GetProfile(Arg.Any<PaintSource>()).Returns(new PaintProfile
            {
                Sources = PaintSource.ProjectileTrail,
                Radius = 0.15f,
                Opacity = 1f,
                ColorMode = PaintColorMode.Palette,
                CustomColor = Color.white
            });

            _settings.InkOnPaper.Returns(true);
            _settings.PaperStrokeScale.Returns(1.6f);
            _scenario = new ActiveScenario();

            _display = Substitute.For<IGameDisplayConfiguration>();
            _palette = Substitute.For<IGamePalette>();

            // Set up a 3-color palette.
            var entries = new List<PaletteEntry> { CreateEntry("Red", Color.red), CreateEntry("Blue", Color.blue), CreateEntry("Green", Color.green) };
            _palette.Colors.Returns(entries);

            _levelUpDismissedSubscriber = Substitute.For<ISubscriber<LevelUpDismissedMessage>>();
            _levelUpDismissedSubscriber
                .Subscribe(Arg.Any<IMessageHandler<LevelUpDismissedMessage>>(), Arg.Any<MessageHandlerFilter<LevelUpDismissedMessage>[]>())
                .Returns(Substitute.For<System.IDisposable>());

            var gameOverSubscriber = Substitute.For<ISubscriber<GameOverMessage>>();
            gameOverSubscriber
                .Subscribe(Arg.Any<IMessageHandler<GameOverMessage>>(), Arg.Any<MessageHandlerFilter<GameOverMessage>[]>())
                .Returns(Substitute.For<System.IDisposable>());

            var runResetSubscriber = Substitute.For<ISubscriber<RunResetMessage>>();
            runResetSubscriber
                .Subscribe(Arg.Any<IMessageHandler<RunResetMessage>>(), Arg.Any<MessageHandlerFilter<RunResetMessage>[]>())
                .Returns(Substitute.For<System.IDisposable>());

            _transitionCompleted = null;
            var transitionCompletedSubscriber = Substitute.For<ISubscriber<LevelTransitionCompletedMessage>>();
            transitionCompletedSubscriber
                .Subscribe(Arg.Do<IMessageHandler<LevelTransitionCompletedMessage>>(h => _transitionCompleted = h),
                    Arg.Any<MessageHandlerFilter<LevelTransitionCompletedMessage>[]>())
                .Returns(Substitute.For<System.IDisposable>());

            _service = new SmokeFieldService(_settings, _display, _palette, _levelUpDismissedSubscriber, gameOverSubscriber,
                runResetSubscriber, transitionCompletedSubscriber, _scenario);
        }

        [TearDown]
        public void TearDown()
        {
            ((System.IDisposable)_service).Dispose();
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

        // --- Paper: the ink field ---

        [Test]
        public void Paper_Inks()
        {
            Start();

            _scenario.Reveal(ScenarioKind.Paper, 4f);

            Assert.IsTrue(_service.IsInking);
            Assert.AreEqual(1f, Shader.GetGlobalFloat(InkActiveId));
        }

        [Test]
        public void LeavingPaper_StopsInking_ButTheLeavingPageKeepsItsInk()
        {
            _scenario.Cut(ScenarioKind.Paper);
            Start();

            _scenario.Reveal(ScenarioKind.Ground, 4f);

            Assert.IsFalse(_service.IsInking);
            Assert.AreEqual(1f, Shader.GetGlobalFloat(InkActiveId), "the page swaps out in its colours, not blank");
        }

        [Test]
        public void TransitionSettled_AfterLeavingPaper_ClearsTheInk()
        {
            _scenario.Cut(ScenarioKind.Paper);
            Start();
            _scenario.Reveal(ScenarioKind.Ground, 4f);

            _transitionCompleted.Handle(new LevelTransitionCompletedMessage());

            Assert.AreEqual(0f, Shader.GetGlobalFloat(InkActiveId));
        }

        [Test]
        public void TransitionSettled_OnASecondPaperLevel_KeepsInking()
        {
            _scenario.Cut(ScenarioKind.Paper);
            Start();

            _transitionCompleted.Handle(new LevelTransitionCompletedMessage());

            Assert.IsTrue(_service.IsInking);
            Assert.AreEqual(1f, Shader.GetGlobalFloat(InkActiveId));
        }

        [Test]
        public void OtherScenarios_DontInk()
        {
            Start();

            foreach (var kind in new[] { ScenarioKind.Sky, ScenarioKind.Ground, ScenarioKind.Water })
            {
                _scenario.Cut(kind);
                Assert.IsFalse(_service.IsInking, kind.ToString());
            }
        }

        [Test]
        public void Paper_WithInkOff_DoesntInk()
        {
            _settings.InkOnPaper.Returns(false);
            Start();

            _scenario.Cut(ScenarioKind.Paper);

            Assert.IsFalse(_service.IsInking);
            Assert.AreEqual(0f, Shader.GetGlobalFloat(InkActiveId));
        }

        // --- Paint rejection (resources not ready) ---

        [Test]
        public void Paint_WhenResourcesNotReady_DoesNotQueue()
        {
            // Resources are never initialized so IsReady == false.
            _service.Paint(PaintSource.ProjectileTrail, Vector3.zero, 0);

            Assert.AreEqual(0, GetPendingStampCount());
        }

        [Test]
        public void Paint_NegativePaletteIndex_DoesNotQueue()
        {
            // Even if resources were ready, invalid index should reject.
            _service.Paint(PaintSource.ProjectileTrail, Vector3.zero, -1);

            Assert.AreEqual(0, GetPendingStampCount());
        }

        [Test]
        public void Paint_PaletteIndexOutOfRange_DoesNotQueue()
        {
            // Palette has 3 entries (indices 0-2); index 3 is OOB.
            _service.Paint(PaintSource.ProjectileTrail, Vector3.zero, 3);

            Assert.AreEqual(0, GetPendingStampCount());
        }

        // --- Tick early-exit when resources not ready ---

        [Test]
        public void Tick_WhenResourcesNotReady_DoesNotAccumulateSmokeTime()
        {
            ((ITickable)_service).Tick();

            Assert.AreEqual(0f, GetSmokeTime(), 0.001f);
        }

        // --- Helpers ---

        // Without stamp and decay shaders the field stays disabled, but the paper switch still follows the scenario.
        private void Start()
        {
            ((IStartable)_service).Start();
        }

        private float GetWindDampen()
        {
            var field = typeof(SmokeFieldService).GetField("_windDampen", BindingFlags.NonPublic | BindingFlags.Instance);
            return (float)field!.GetValue(_service);
        }

        private int GetPendingStampCount()
        {
            var field = typeof(SmokeFieldService).GetField("_pendingStamps", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<object>)null;
            var raw = field!.GetValue(_service);
            // Use IList to get count from the generic list regardless of inner type.
            return ((System.Collections.IList)raw).Count;
        }

        private float GetSmokeTime()
        {
            var field = typeof(SmokeFieldService).GetField("_smokeTime", BindingFlags.NonPublic | BindingFlags.Instance);
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
