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

        private static readonly PurpleHeartStep[] PurpleHearts =
        {
            new(0.3f, 1f),
            new(0.6f, 0.5f),
            new(0.9f, 0.25f),
        };

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
            _settings.PurpleHearts.Returns(PurpleHearts);
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
        public void Golden_OwedButAnotherIsStillOnTheBoard_NotDueUntilItLeaves()
        {
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            var left = _service.Placed(BalloonType.Golden);
            _roll = 0f;
            _service.OnHit(Hit(Golden(), HitOutcome.Pop));

            Assert.IsFalse(LinesUntilDue(RepeatDelay * 3), "the owed one waits while a golden is on the board");
            left();
            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        [Test]
        public void Golden_PopOfAnotherBalloonType_RollsNothing()
        {
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            var left = _service.Placed(BalloonType.Golden);
            var other = Substitute.For<IBalloonModel>();
            other.TypeName.Returns(BalloonType.Heart);
            _roll = 0f;

            _service.OnHit(Hit(other, HitOutcome.Pop));
            left();

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void Golden_NoRepeatChancesConfigured_RollsNothingAndDoesNotThrow()
        {
            _roll = 0f;
            foreach (var chances in new float[][] { null, new float[0] })
            {
                _settings.GoldenRepeatChances.Returns(chances);
                _service.ResetRun(1);

                Assert.DoesNotThrow(() => PlaceAndPopAGolden(roll: 0f));
                Assert.IsFalse(LinesUntilDue(20));
            }
        }

        [Test]
        public void Golden_OwedButNotYetDue_TheHeartStillRollsOnTheLinesBetween()
        {
            PlaceAndPopAGolden(roll: 0f);
            _hearts.Value = StartingHearts - 1;
            _roll = 0f;

            Assert.IsTrue(_service.TryTakeDue(out var first));
            Assert.AreEqual(BalloonType.Heart, first);
            _service.Placed(BalloonType.Heart);

            Assert.IsFalse(_service.TryTakeDue(out _), "line 2: heart on the board, golden not yet due");
            Assert.IsTrue(_service.TryTakeDue(out var third));
            Assert.AreEqual(BalloonType.Golden, third, "the golden is still owed on its due line");
        }

        [Test]
        public void Golden_PopArrivingBeforeItsLeave_StillRolls()
        {
            SetCompletion(0.8f);
            _service.TryTakeDue(out _);
            var left = _service.Placed(BalloonType.Golden);
            _roll = 0f;

            _service.OnHit(Hit(Golden(), HitOutcome.Pop));
            left();

            Assert.IsTrue(LinesUntilDue(RepeatDelay));
        }

        [Test]
        public void PurpleHeart_FullHearts_TheFirstComesAtItsCompletion()
        {
            _roll = 1f;
            SetCompletion(0.29f);
            Assert.IsFalse(_service.TryTakeDue(out _));

            SetCompletion(0.3f);

            Assert.IsTrue(_service.TryTakeDue(out var type), "the first step is sure");
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [TestCase(0.6f, 0.49f, true)]
        [TestCase(0.6f, 0.5f, false)]
        public void PurpleHeart_TheSecondRollsItsChanceAtItsCompletion(float completion, float roll, bool due)
        {
            PlaceAndLeaveAPurpleHeart(0.3f);
            _roll = roll;
            SetCompletion(completion);

            Assert.AreEqual(due, _service.TryTakeDue(out _));
        }

        [TestCase(0.24f, true)]
        [TestCase(0.25f, false)]
        public void PurpleHeart_TheThirdRollsAQuarterAtNinety(float roll, bool due)
        {
            PlaceAndLeaveAPurpleHeart(0.3f);
            _roll = 0f;
            PlaceAndLeaveAPurpleHeart(0.6f);
            _roll = roll;
            SetCompletion(0.9f);

            Assert.AreEqual(due, _service.TryTakeDue(out _));
        }

        [Test]
        public void PurpleHeart_EachStepRollsOnce()
        {
            PlaceAndLeaveAPurpleHeart(0.3f);
            _roll = 0.9f;
            SetCompletion(0.6f);
            Assert.IsFalse(_service.TryTakeDue(out _));

            _roll = 0f;

            Assert.IsFalse(HeartWithin(20), "the 60% step lost its roll; the next is at 90%");
        }

        [Test]
        public void PurpleHeart_ThreeAtMost()
        {
            _roll = 0f;
            PlaceAndLeaveAPurpleHeart(0.3f);
            PlaceAndLeaveAPurpleHeart(0.6f);
            PlaceAndLeaveAPurpleHeart(0.9f);
            SetCompletion(1f);

            Assert.IsFalse(HeartWithin(20));
        }

        [Test]
        public void PurpleHeart_WonWhileOneIsOnTheBoard_ComesOnceItLeaves()
        {
            _roll = 0f;
            SetCompletion(0.3f);
            _service.TryTakeDue(out _);
            var left = _service.Placed(BalloonType.Heart);
            SetCompletion(0.6f);

            Assert.IsFalse(HeartWithin(5), "one heart on the board at a time");
            left();
            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [Test]
        public void PurpleHeart_ABigJump_RollsEveryStepItPasses_AndBringsThemOneAtATime()
        {
            _roll = 0f;
            SetCompletion(0.95f);

            for (var heart = 1; heart <= PurpleHearts.Length; heart++)
            {
                Assert.IsTrue(_service.TryTakeDue(out var type), $"heart {heart}");
                Assert.AreEqual(BalloonType.Heart, type);
                _service.Placed(BalloonType.Heart)();
            }

            Assert.IsFalse(HeartWithin(20));
        }

        [Test]
        public void PurpleHeart_TheGoldenTakesTheLine_TheStepsRollOnTheNextAndAreNotLost()
        {
            _roll = 1f;
            SetCompletion(0.8f);
            Assert.IsTrue(_service.TryTakeDue(out var first));
            Assert.AreEqual(BalloonType.Golden, first);
            _service.Placed(BalloonType.Golden);

            _roll = 0f;

            Assert.IsTrue(_service.TryTakeDue(out var second), "the sure 30% step");
            Assert.AreEqual(BalloonType.Heart, second);
            _service.Placed(BalloonType.Heart)();
            Assert.IsTrue(_service.TryTakeDue(out var third), "the 60% step, rolled on this later line, won");
            Assert.AreEqual(BalloonType.Heart, third);
            _service.Placed(BalloonType.Heart)();
            Assert.IsFalse(HeartWithin(20), "the 90% step is not reached");
        }

        [Test]
        public void PurpleHeart_AllThreeWonWhileTheGoldenTookTheLine_AllComeOneAtATime()
        {
            _roll = 0f;
            SetCompletion(0.95f);
            Assert.IsTrue(_service.TryTakeDue(out var first));
            Assert.AreEqual(BalloonType.Golden, first);
            _service.Placed(BalloonType.Golden);

            var hearts = 0;
            for (var line = 0; line < 10; line++)
            {
                if (_service.TryTakeDue(out var type) && type == BalloonType.Heart)
                {
                    hearts++;
                    _service.Placed(BalloonType.Heart)();
                }
            }

            Assert.AreEqual(PurpleHearts.Length, hearts);
        }

        [Test]
        public void PurpleHeart_PlacedWithNothingOwed_TheFirstStepStillBringsItsHeart()
        {
            _service.Placed(BalloonType.Heart)();
            SetCompletion(0.1f);
            Assert.IsFalse(HeartWithin(5));

            SetCompletion(0.3f);

            Assert.IsTrue(_service.TryTakeDue(out var type), "a spawn-cheat heart doesn't use up the owed count");
            Assert.AreEqual(BalloonType.Heart, type);
            _service.Placed(BalloonType.Heart)();
            Assert.IsFalse(HeartWithin(20), "exactly one was owed");
        }

        [Test]
        public void PurpleHeart_TheRollWhileHurtIsOff()
        {
            _hearts.Value = 1;
            _roll = 0f;
            SetCompletion(0.1f);

            Assert.IsFalse(HeartWithin(Cooldown * 3));
        }

        [Test]
        public void PurpleHeart_NextLevel_DropsWhatWasOwedAndStartsTheScheduleOver()
        {
            _roll = 0f;
            SetCompletion(0.95f);
            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Heart)();
            _levelNumber.Value = 2;
            SetCompletion(0.1f);

            Assert.IsFalse(HeartWithin(5), "the hearts the last level won are gone");
            SetCompletion(0.3f);
            Assert.IsTrue(_service.TryTakeDue(out _), "the new level's first step");
        }

        [Test]
        public void PurpleHeart_RunReset_StartsTheScheduleOver()
        {
            _roll = 0f;
            PlaceAndLeaveAPurpleHeart(0.3f);
            SetCompletion(0.95f);
            _service.TryTakeDue(out _);

            _service.ResetRun(1);
            SetCompletion(0.1f);

            Assert.IsFalse(HeartWithin(5));
            SetCompletion(0.3f);
            Assert.IsTrue(_service.TryTakeDue(out _));
        }

        [Test]
        public void PurpleHeart_StepsListedOutOfOrder_EachRollsAtItsOwnCompletion()
        {
            _settings.PurpleHearts.Returns(new[] { PurpleHearts[2], PurpleHearts[1], PurpleHearts[0] });
            _roll = 1f;
            SetCompletion(0.3f);

            Assert.IsTrue(_service.TryTakeDue(out var type), "the sure 30% step, though listed last");
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [Test]
        public void PurpleHeart_NoScheduleConfigured_NoHeartAndNoThrow()
        {
            _roll = 0f;
            foreach (var steps in new[] { null, new PurpleHeartStep[0] })
            {
                _settings.PurpleHearts.Returns(steps);
                _service.ResetRun(1);
                SetCompletion(0.95f);

                Assert.DoesNotThrow(() => Assert.IsFalse(HeartWithin(5)));
            }
        }

        [Test]
        public void Heart_FullHearts_NeverDue()
        {
            _level.IsExtraColorLevel.Returns(false);
            _roll = 0f;

            for (var i = 0; i < 20; i++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _));
            }
        }

        [Test]
        public void Heart_PastTheMaxFromExtraHearts_NeverDueOffPurple()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = StartingHearts + 2;
            _roll = 0f;

            for (var i = 0; i < Cooldown * 3; i++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _));
            }
        }

        [Test]
        public void Heart_PendingButExtraHeartsPutItPastTheMax_NoLongerDue()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = StartingHearts - 1;
            _roll = 0f;
            Assert.IsTrue(_service.TryTakeDue(out _));

            _hearts.Value = StartingHearts + 1;

            Assert.IsFalse(_service.TryTakeDue(out _));
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
            _level.IsExtraColorLevel.Returns(false);
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
        public void NoGoldenEntry_ALatePurpleLineStillBringsTheHeart()
        {
            _service.Dispose();
            _service = CreateService(BalloonType.Heart);
            _hearts.Value = 1;
            _roll = 0f;
            SetCompletion(0.9f);

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [TestCase(0, 0f)]
        [TestCase(-1, 0f)]
        [TestCase(1, 0.04f)]
        [TestCase(2, 0.12f)]
        [TestCase(3, 0.12f)]
        public void HeartChance_ByMissingHearts(int missing, float expected)
        {
            Assert.AreEqual(expected, RewardBalloonService.HeartChance(_settings, missing), 1e-5f);
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
            // The game's order: the pop's leave runs before its hit is published (HitPipeline).
            left();
            _service.OnHit(Hit(golden, HitOutcome.Pop));
            return golden;
        }

        // The purple level's next heart balloon, due at the given completion, placed and gone again (any way off the
        // board: a purple heart needs no pop for the next).
        private void PlaceAndLeaveAPurpleHeart(float completion)
        {
            SetCompletion(completion);
            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Heart, type);
            _service.Placed(BalloonType.Heart)();
        }

        // Whether a heart balloon falls due within the given number of lines.
        private bool HeartWithin(int lines)
        {
            for (var line = 0; line < lines; line++)
            {
                if (_service.TryTakeDue(out var type) && type == BalloonType.Heart)
                {
                    return true;
                }
            }

            return false;
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
