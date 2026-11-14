using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Reward;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class RewardBalloonServiceTests
    {
        private const int StartingHearts = 3;
        private const int Required = 100;
        private const int Cooldown = 4;
        private const int RepeatDelay = 3;

        private static readonly string[] Colors = { "Red", "Blue", "Purple" };
        private static readonly float[] RepeatChances = { 0.25f, 0.1f };

        private readonly Dictionary<string, int> _progress = new();

        private IRewardBalloonSettings _settings;
        private ILevelProgress _levelProgress;
        private ILevelParameters _level;
        private ReactiveProperty<int> _levelNumber;
        private ReactiveProperty<LevelUpPhase> _phase;
        private ReactiveProperty<int> _hearts;
        private IActiveLevelParameters _levelParams;
        private IPlayerHealth _health;
        private RewardBalloonService _service;
        private float _roll;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<IRewardBalloonSettings>();
            _settings.GoldenAtCompletion.Returns(0.75f);
            _settings.GoldenRepeatChances.Returns(RepeatChances);
            _settings.GoldenRepeatDelayLines.Returns(RepeatDelay);
            _settings.HeartChanceMissingOne.Returns(0.04f);
            _settings.HeartChanceMissingTwo.Returns(0.12f);
            _settings.HeartChanceOnPurple.Returns(0.05f);
            _settings.HeartCooldownLines.Returns(Cooldown);

            _levelNumber = new ReactiveProperty<int>(1);
            _phase = new ReactiveProperty<LevelUpPhase>(LevelUpPhase.Playing);
            _progress.Clear();
            _levelProgress = Substitute.For<ILevelProgress>();
            _levelProgress.Level.Returns(_levelNumber);
            _levelProgress.Phase.Returns(_phase);
            _levelProgress.GetRequiredPoints().Returns(Required);
            _levelProgress.GetProgress(Arg.Any<string>())
                .Returns(call => _progress.TryGetValue(call.Arg<string>(), out var p) ? p : 0);

            _level = Substitute.For<ILevelParameters>();
            _level.AllowedColors.Returns(Colors);
            _level.IsExtraColorLevel.Returns(true);
            _levelParams = Substitute.For<IActiveLevelParameters>();
            _levelParams.Current.Returns(_level);

            _hearts = new ReactiveProperty<int>(StartingHearts);
            _health = Substitute.For<IPlayerHealth>();
            _health.Current.Returns(_hearts);
            _health.Max.Returns(StartingHearts);

            _roll = 1f;
            _service = CreateService(BalloonType.Golden, BalloonType.Heart);
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public void Golden_PurpleLevelBelowTheThreshold_NotDue()
        {
            SetCompletion(0.7f);

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void Golden_PurpleLevelAtTheThreshold_Due()
        {
            SetCompletion(0.75f);

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        [Test]
        public void Golden_NormalLevel_NeverDue()
        {
            _level.IsExtraColorLevel.Returns(false);
            SetCompletion(0.95f);

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void Golden_OutsidePlaying_NotDue()
        {
            SetCompletion(0.9f);
            _phase.Value = LevelUpPhase.Pending;

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void Golden_NotPlaced_StaysDue()
        {
            SetCompletion(0.8f);

            Assert.IsTrue(_service.TryTakeDue(out _));
            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        [Test]
        public void Golden_OncePerLevel_EvenAfterItLeaves()
        {
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            var left = _service.Placed(BalloonType.Golden);
            left();

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void Golden_NextLevel_DueAgain()
        {
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Golden)();

            _levelNumber.Value = 2;

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        [Test]
        public void Golden_StillOnTheBoardIntoTheNextLevel_NotDueUntilItLeaves()
        {
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            var left = _service.Placed(BalloonType.Golden);
            _levelNumber.Value = 2;

            Assert.IsFalse(_service.TryTakeDue(out _));
            left();
            Assert.IsTrue(_service.TryTakeDue(out _));
        }

        [Test]
        public void Golden_AfterRunReset_DueAgain()
        {
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Golden);

            _service.ResetRun(1);

            Assert.IsTrue(_service.TryTakeDue(out _));
        }

        [Test]
        public void Golden_PopWinsTheRoll_TheNextComesAfterTheDelay()
        {
            PlaceAndPopAGolden(roll: 0.2f);

            for (var line = 1; line < RepeatDelay; line++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _), $"line {line}");
            }

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        [Test]
        public void Golden_PopLosesTheRoll_NoMoreThisLevel()
        {
            PlaceAndPopAGolden(roll: 0.3f);

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void Golden_TheThirdRollsAgainstTheSecondChance()
        {
            PlaceAndPopAGolden(roll: 0.2f);
            Assert.IsTrue(LinesUntilDue(RepeatDelay));

            PlaceAndPopAGolden(roll: 0.2f);

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void Golden_ThreeAtMost_AWinningRollForAFourthBringsNone()
        {
            PlaceAndPopAGolden(roll: 0f);
            Assert.IsTrue(LinesUntilDue(RepeatDelay));
            PlaceAndPopAGolden(roll: 0f);
            Assert.IsTrue(LinesUntilDue(RepeatDelay));

            PlaceAndPopAGolden(roll: 0f);

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void Golden_LeavesWithoutAPop_RollsNothing()
        {
            _roll = 0f;
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Golden)();

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void Golden_OnePopReportedTwice_RollsOnce()
        {
            var golden = PlaceAndPopAGolden(roll: 0.5f);

            _roll = 0f;
            _service.OnHit(Hit(golden, HitOutcome.Pop));

            Assert.IsFalse(LinesUntilDue(20));
        }

        [TestCase(HitOutcome.Deflect)]
        [TestCase(HitOutcome.None)]
        public void Golden_AHitThatIsNoPop_RollsNothing(HitOutcome outcome)
        {
            _roll = 0f;
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            var left = _service.Placed(BalloonType.Golden);
            _service.OnHit(Hit(Golden(), outcome));
            left();

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void Golden_PoppedAfterItsLevelEnded_RollsNothingForTheNext()
        {
            _roll = 0f;
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            var left = _service.Placed(BalloonType.Golden);
            _levelNumber.Value = 2;
            SetCompletion(0.5f);

            _service.OnHit(Hit(Golden(), HitOutcome.Pop));
            left();

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void Golden_WonButTheLevelEnds_NotOwedToTheNextLevel()
        {
            PlaceAndPopAGolden(roll: 0f);
            _levelNumber.Value = 2;
            SetCompletion(0.5f);

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void Golden_WonButNotPlaced_StaysDue()
        {
            PlaceAndPopAGolden(roll: 0f);
            Assert.IsTrue(LinesUntilDue(RepeatDelay));

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        [Test]
        public void Golden_WonThenARunReset_OnlyTheFirstThresholdBringsOne()
        {
            PlaceAndPopAGolden(roll: 0f);
            _service.ResetRun(1);
            SetCompletion(0.5f);

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void Heart_FullHearts_NeverDue()
        {
            _roll = 0f;

            for (var i = 0; i < 20; i++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _));
            }
        }

        [Test]
        public void Heart_MissingOne_RollUnderTheChance_Due()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = StartingHearts - 1;
            _roll = 0.039f;

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [Test]
        public void Heart_MissingOne_RollAtTheChance_NotDue()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = StartingHearts - 1;
            _roll = 0.04f;

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void Heart_WhileOneIsOnTheBoard_NotDue()
        {
            _hearts.Value = 1;
            _roll = 0f;
            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Heart);

            for (var i = 0; i < Cooldown * 3; i++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _));
            }
        }

        [Test]
        public void Heart_AfterItLeaves_WaitsTheCooldownLines()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = 1;
            _roll = 0f;
            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Heart)();

            for (var i = 1; i < Cooldown; i++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _), $"line {i} of the cooldown");
            }

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [Test]
        public void Heart_LeaveCallbackFromBeforeARunReset_IsIgnored()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = 1;
            _roll = 0f;
            _service.TryTakeDue(out _);
            var staleLeft = _service.Placed(BalloonType.Heart);

            _service.ResetRun(1);
            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Heart);
            staleLeft();

            for (var i = 0; i < Cooldown * 3; i++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _), "the new run's heart is still on the board");
            }
        }

        [Test]
        public void Heart_RunReset_FirstLineCanRoll()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = 1;
            _roll = 0f;
            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Heart)();

            _service.ResetRun(1);

            Assert.IsTrue(_service.TryTakeDue(out _));
        }

        [Test]
        public void Golden_WinsOverAHeartOnTheSameLine()
        {
            _hearts.Value = 1;
            _roll = 0f;
            SetCompletion(0.8f);

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        [Test]
        public void Heart_RolledButNotPlaced_StaysDueWithoutRollingAgain()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = StartingHearts - 1;
            _roll = 0f;
            Assert.IsTrue(_service.TryTakeDue(out _));

            _roll = 1f;

            Assert.IsTrue(_service.TryTakeDue(out var type), "the line that had no room for it owes it");
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [Test]
        public void Heart_PendingButHealedToFull_NoLongerDue()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = StartingHearts - 1;
            _roll = 0f;
            _service.TryTakeDue(out _);

            _hearts.Value = StartingHearts;

            Assert.IsFalse(_service.TryTakeDue(out _));
            _hearts.Value = StartingHearts - 1;
            _roll = 1f;
            Assert.IsFalse(_service.TryTakeDue(out _), "the pending heart was dropped, so this line rolls afresh");
        }

        [Test]
        public void Heart_AnEarlierHeartsLeaveCallback_DoesNotClearTheOneOnTheBoard()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = 1;
            _roll = 0f;
            _service.TryTakeDue(out _);
            var firstLeft = _service.Placed(BalloonType.Heart);
            firstLeft();
            for (var i = 0; i < Cooldown; i++)
            {
                _service.TryTakeDue(out _);
            }

            _service.Placed(BalloonType.Heart);
            firstLeft();

            for (var i = 0; i < Cooldown * 3; i++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _), "the second heart is still on the board");
            }
        }

        [Test]
        public void NoCatalogueEntries_NothingIsEverDue()
        {
            _service.Dispose();
            _service = CreateService();
            _hearts.Value = 1;
            _roll = 0f;
            SetCompletion(0.9f);

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void NoGoldenEntry_ALatePurpleLineStillRollsTheHeart()
        {
            _service.Dispose();
            _service = CreateService(BalloonType.Heart);
            _hearts.Value = 1;
            _roll = 0f;
            SetCompletion(0.9f);

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [TestCase(0, false, 0f)]
        [TestCase(-1, false, 0f)]
        [TestCase(0, true, 0f)]
        [TestCase(1, false, 0.04f)]
        [TestCase(1, true, 0.09f)]
        [TestCase(2, false, 0.12f)]
        [TestCase(3, true, 0.17f)]
        public void HeartChance_ByMissingHeartsAndLevel(int missing, bool purple, float expected)
        {
            Assert.AreEqual(expected, RewardBalloonService.HeartChance(_settings, missing, purple), 1e-5f);
        }

        [Test]
        public void LevelCompletion_CapsEachColourAtItsGoal()
        {
            _progress["Red"] = 300;
            _progress["Blue"] = 0;
            _progress["Purple"] = 50;

            Assert.AreEqual(0.5f, LevelCompletion.Of(_levelProgress, Colors), 1e-5f);
        }

        [Test]
        public void LevelCompletion_EveryGoalMet_IsOne()
        {
            SetCompletion(1f);

            Assert.AreEqual(1f, LevelCompletion.Of(_levelProgress, Colors), 1e-5f);
        }

        [Test]
        public void LevelCompletion_NoGoalOrNoColours_IsZero()
        {
            SetCompletion(1f);

            Assert.AreEqual(0f, LevelCompletion.Of(_levelProgress, new string[0]));
            _levelProgress.GetRequiredPoints().Returns(0);
            Assert.AreEqual(0f, LevelCompletion.Of(_levelProgress, Colors));
        }

        private static GoldenBalloonModel Golden()
        {
            return new GoldenBalloonModel(new BalloonModelConfig(BalloonType.Golden));
        }

        private static ActorHitMessage Hit(IBalloonModel balloon, HitOutcome outcome)
        {
            var context = new DamageContext(1, DamageFlags.Piercing | DamageFlags.DirectHit);
            return new ActorHitMessage(balloon, Vector3.zero, Vector3.up, outcome, context);
        }

        private static BalloonPrefabEntry Entry(BalloonType type)
        {
            var entry = new BalloonPrefabEntry();
            typeof(BalloonPrefabEntry).GetField("_balloonType", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(entry, type);
            return entry;
        }

        private RewardBalloonService CreateService(params BalloonType[] catalogue)
        {
            var entries = new List<BalloonPrefabEntry>();
            foreach (var type in catalogue)
            {
                entries.Add(Entry(type));
            }

            var balloons = Substitute.For<IBalloonsConfiguration>();
            balloons.RewardBalloons.Returns(_settings);
            balloons.Entries.Returns(entries);

            var service = new RewardBalloonService(
                balloons, _levelProgress, _levelParams, _health, Substitute.For<ISubscriber<ActorHitMessage>>())
            {
                Random01 = () => _roll,
            };
            service.Start();
            return service;
        }

        // The level's next golden balloon placed and popped, its pop rolling the given number. The first comes from
        // the completion threshold, a later one from the roll its predecessor won.
        private GoldenBalloonModel PlaceAndPopAGolden(float roll)
        {
            SetCompletion(0.8f);
            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
            var left = _service.Placed(BalloonType.Golden);
            var golden = Golden();
            _roll = roll;
            _service.OnHit(Hit(golden, HitOutcome.Pop));
            left();
            return golden;
        }

        // Whether a golden balloon falls due within the given number of lines, stopping at the first that does.
        private bool LinesUntilDue(int lines)
        {
            for (var line = 0; line < lines; line++)
            {
                if (_service.TryTakeDue(out var type) && type == BalloonType.Golden)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetCompletion(float fraction)
        {
            foreach (var color in Colors)
            {
                _progress[color] = (int)System.Math.Round(Required * fraction);
            }
        }
    }
}
