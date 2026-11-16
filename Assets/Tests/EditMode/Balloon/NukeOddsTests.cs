using System;
using BalloonParty.Balloon.Reward;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Game.Health;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class NukeOddsTests
    {
        private const float Tolerance = 1e-5f;
        private const float FillWeight = 0.4f;
        private const float PressureWeight = 0.3f;
        private const float HeartsWeight = 0.2f;
        private const float LossWeight = 0.1f;
        private const float BaseChance = 0.01f;
        private const float MaxChance = 0.5f;
        private const int MaxHearts = 5;

        private INukeOddsSettings _settings;
        private IBoardFill _boardFill;
        private ISpawnClock _clock;
        private ReactiveProperty<int> _hearts;
        private IPlayerHealth _health;
        private ISubscriber<WaveDamageMessage> _damageSubscriber;
        private IMessageHandler<WaveDamageMessage> _damageHandler;
        private NukeOdds _odds;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<INukeOddsSettings>();
            _settings.BaseChance.Returns(BaseChance);
            _settings.MaxChance.Returns(MaxChance);
            _settings.FillFrom.Returns(0.5f);
            _settings.FillFull.Returns(0.9f);
            _settings.FillWeight.Returns(FillWeight);
            _settings.PressureWeight.Returns(PressureWeight);
            _settings.HeartsWeight.Returns(HeartsWeight);
            _settings.LossWeight.Returns(LossWeight);
            _settings.LossesForFull.Returns(2f);
            _settings.LossKeptPerLine.Returns(0.5f);

            _boardFill = Substitute.For<IBoardFill>();
            _clock = Substitute.For<ISpawnClock>();
            _hearts = new ReactiveProperty<int>(MaxHearts);
            _health = Substitute.For<IPlayerHealth>();
            _health.Current.Returns(_hearts);
            _health.Max.Returns(MaxHearts);

            _damageHandler = null;
            _damageSubscriber = Substitute.For<ISubscriber<WaveDamageMessage>>();
            _damageSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<WaveDamageMessage>>(h => _damageHandler = h),
                    Arg.Any<MessageHandlerFilter<WaveDamageMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.NukeOdds.Returns(_settings);
            _odds = new NukeOdds(balloons, _boardFill, _clock, _health, _damageSubscriber);
            _odds.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _odds.Dispose();
        }

        [Test]
        public void Urgency_NoTrouble_IsZero()
        {
            Assert.AreEqual(0f, _odds.Urgency, Tolerance);
            Assert.AreEqual(BaseChance, _odds.Chance, Tolerance);
        }

        [Test]
        public void Urgency_FillAlone_IsItsWeight()
        {
            _boardFill.Occupancy.Returns(0.9f);

            Assert.AreEqual(FillWeight, _odds.Urgency, Tolerance);
        }

        [Test]
        public void Urgency_PressureAlone_IsItsWeight()
        {
            _clock.Pressure.Returns(1f);

            Assert.AreEqual(PressureWeight, _odds.Urgency, Tolerance);
        }

        [Test]
        public void Urgency_HeartsAlone_IsItsWeightOnTheLastHeart()
        {
            _hearts.Value = 1;

            Assert.AreEqual(HeartsWeight, _odds.Urgency, Tolerance);
        }

        [Test]
        public void Urgency_HeartsHalfGone_ReadsHalfTheWeight()
        {
            _hearts.Value = 3;

            Assert.AreEqual(HeartsWeight * 0.5f, _odds.Urgency, Tolerance);
        }

        [Test]
        public void Urgency_AllSignalsAtOnce_IsTheirSum()
        {
            _boardFill.Occupancy.Returns(0.9f);
            _clock.Pressure.Returns(1f);
            _hearts.Value = 1;
            _damageHandler.Handle(new WaveDamageMessage(2, 12, 6));

            Assert.AreEqual(1f, _odds.Urgency, Tolerance);
        }

        [Test]
        public void UrgencyOf_WeightsSumPastOne_CapsAtOne()
        {
            _settings.FillWeight.Returns(0.8f);
            _settings.PressureWeight.Returns(0.8f);

            Assert.AreEqual(1f, NukeOdds.UrgencyOf(_settings, 1f, 1f, 1f, 5f), Tolerance);
        }

        [Test]
        public void UrgencyOf_SignalsPastTheirRange_AreClamped()
        {
            var urgency = NukeOdds.UrgencyOf(_settings, 0f, 7f, 0f, 0f);

            Assert.AreEqual(PressureWeight, urgency, Tolerance);
        }

        [TestCase(0.3f, 0f)]
        [TestCase(0.5f, 0f)]
        [TestCase(0.7f, 0.5f)]
        [TestCase(0.9f, 1f)]
        [TestCase(1f, 1f)]
        public void FillSignal_RisesFromFillFromToFillFull(float occupancy, float expected)
        {
            Assert.AreEqual(expected, NukeOdds.FillSignal(_settings, occupancy), Tolerance);
        }

        // With no range to rise over the signal is a step at FillFrom, not a division by zero.
        [TestCase(0.5f, 0.5f, 0.49f, 0f)]
        [TestCase(0.5f, 0.5f, 0.5f, 1f)]
        [TestCase(0.6f, 0.5f, 0.55f, 0f)]
        [TestCase(0.6f, 0.5f, 0.6f, 1f)]
        public void FillSignal_FullAtOrBelowFrom_IsAStepAtFrom(float from, float full, float occupancy, float expected)
        {
            _settings.FillFrom.Returns(from);
            _settings.FillFull.Returns(full);

            Assert.AreEqual(expected, NukeOdds.FillSignal(_settings, occupancy), Tolerance);
        }

        [Test]
        public void ChanceFor_NoUrgency_IsTheBase()
        {
            Assert.AreEqual(BaseChance, NukeOdds.ChanceFor(_settings, 0f), Tolerance);
        }

        [Test]
        public void ChanceFor_FullUrgency_IsTheMax()
        {
            Assert.AreEqual(MaxChance, NukeOdds.ChanceFor(_settings, 1f), Tolerance);
        }

        [Test]
        public void ChanceFor_HalfUrgency_IsHalfwayBetween()
        {
            Assert.AreEqual((BaseChance + MaxChance) * 0.5f, NukeOdds.ChanceFor(_settings, 0.5f), Tolerance);
        }

        [Test]
        public void ChanceFor_UrgencyOutOfRange_StaysBetweenBaseAndMax()
        {
            Assert.AreEqual(BaseChance, NukeOdds.ChanceFor(_settings, -3f), Tolerance);
            Assert.AreEqual(MaxChance, NukeOdds.ChanceFor(_settings, 3f), Tolerance);
        }

        [Test]
        public void Chance_ReadsTheLiveSignals()
        {
            Assert.AreEqual(BaseChance, _odds.Chance, Tolerance);

            _clock.Pressure.Returns(1f);

            Assert.AreEqual(
                NukeOdds.ChanceFor(_settings, PressureWeight), _odds.Chance, Tolerance);
        }

        [Test]
        public void RecentLosses_OneHeartOfTwoForFull_ReadsHalfTheWeight()
        {
            _damageHandler.Handle(new WaveDamageMessage(1, 6, 6));

            Assert.AreEqual(1f, _odds.RecentLosses, Tolerance);
            Assert.AreEqual(LossWeight * 0.5f, _odds.Urgency, Tolerance);
        }

        [Test]
        public void RecentLosses_TwoHeartsInOneWave_ReadFull()
        {
            _damageHandler.Handle(new WaveDamageMessage(2, 12, 6));

            Assert.AreEqual(LossWeight, _odds.Urgency, Tolerance);
        }

        // One heart after another: the first has faded by half when the second lands, but the sum is well past a single loss.
        [Test]
        public void RecentLosses_TwoLinesInARow_AddUpToNearFull()
        {
            _damageHandler.Handle(new WaveDamageMessage(1, 6, 6));
            var single = _odds.Urgency;
            _odds.LinePassed();
            _damageHandler.Handle(new WaveDamageMessage(1, 6, 6));

            Assert.AreEqual(1.5f, _odds.RecentLosses, Tolerance);
            Assert.AreEqual(LossWeight * 0.75f, _odds.Urgency, Tolerance);
            Assert.Greater(_odds.Urgency, single);
        }

        [Test]
        public void LinePassed_FadesTheRecentLossesByTheKeptShare()
        {
            _damageHandler.Handle(new WaveDamageMessage(2, 12, 6));

            _odds.LinePassed();
            Assert.AreEqual(1f, _odds.RecentLosses, Tolerance);

            _odds.LinePassed();
            Assert.AreEqual(0.5f, _odds.RecentLosses, Tolerance);
        }

        [Test]
        public void LinePassed_KeptShareOutOfRange_IsClamped()
        {
            _damageHandler.Handle(new WaveDamageMessage(1, 6, 6));
            _settings.LossKeptPerLine.Returns(3f);
            _odds.LinePassed();
            Assert.AreEqual(1f, _odds.RecentLosses, Tolerance, "a share past 1 doesn't grow them");

            _settings.LossKeptPerLine.Returns(-1f);
            _odds.LinePassed();
            Assert.AreEqual(0f, _odds.RecentLosses, Tolerance, "a negative share doesn't flip them");
        }

        [Test]
        public void RecentLosses_AMessageWithNoHeartsLost_AddsNothing()
        {
            _damageHandler.Handle(new WaveDamageMessage(0, 4, 6));

            Assert.AreEqual(0f, _odds.RecentLosses, Tolerance);
            Assert.AreEqual(0f, _odds.Urgency, Tolerance);
        }

        [Test]
        public void ResetRun_ClearsTheRecentLosses()
        {
            _damageHandler.Handle(new WaveDamageMessage(2, 12, 6));

            _odds.ResetRun(1);

            Assert.AreEqual(0f, _odds.RecentLosses, Tolerance);
            Assert.AreEqual(0f, _odds.Urgency, Tolerance);
        }

        [Test]
        public void Dispose_DropsTheSubscription()
        {
            var subscription = Substitute.For<IDisposable>();
            var subscriber = Substitute.For<ISubscriber<WaveDamageMessage>>();
            subscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<WaveDamageMessage>>(),
                    Arg.Any<MessageHandlerFilter<WaveDamageMessage>[]>())
                .Returns(subscription);
            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.NukeOdds.Returns(_settings);
            var odds = new NukeOdds(balloons, _boardFill, _clock, _health, subscriber);
            odds.Start();

            odds.Dispose();

            subscription.Received(1).Dispose();
        }
    }
}
