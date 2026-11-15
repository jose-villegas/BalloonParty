using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Reward;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Assist;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class RewardBalloonServiceTests
    {
        private const int StartingHearts = 3;
        private const int Required = 100;
        private const int Cooldown = 4;
        private const int RollEvery = 3;

        private static readonly string[] Colors = { "Red", "Blue", "Purple" };
        private static readonly float[] Empty = new float[0];
        private static readonly float[] HeartChances = { 0.4f, 0.3f, 0.2f, 0.12f, 0.06f };
        private static readonly float[] GoldenChances = { 0.35f, 0.2f, 0.1f };

        private readonly Dictionary<string, int> _progress = new();

        private IRewardBalloonSettings _settings;
        private ILevelProgress _levelProgress;
        private ILevelParameters _level;
        private ReactiveProperty<int> _levelNumber;
        private ReactiveProperty<LevelUpPhase> _phase;
        private ReactiveProperty<int> _hearts;
        private IActiveLevelParameters _levelParams;
        private IPlayerHealth _health;
        private IPlayerNeed _need;
        private IAssistSettings _assist;
        private RewardBalloonService _service;
        private float _roll;
        private int _draws;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<IRewardBalloonSettings>();
            _settings.PurpleGoldenAtCompletion.Returns(0.75f);
            _settings.PurpleRollEveryLines.Returns(1);
            // A sure single golden and no purple hearts by default, so a golden test sees only the golden; a purple
            // roll test sets its own lists (UsePurple).
            _settings.PurpleGoldenChances.Returns(new[] { 1f });
            _settings.PurpleHeartChances.Returns(Empty);
            _settings.HeartChanceMissingOne.Returns(0.04f);
            _settings.HeartChanceMissingTwo.Returns(0.12f);
            _settings.HeartCooldownLines.Returns(Cooldown);
            _settings.GoldenAtCompletion.Returns(0.75f);

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
            _need = Substitute.For<IPlayerNeed>();
            var pity = Substitute.For<IPitySettings>();
            pity.GrowthPerMiss.Returns(0.5f);
            pity.MaxChance.Returns(0.5f);
            _assist = Substitute.For<IAssistSettings>();
            _assist.HeartPity.Returns(pity);

            _roll = 1f;
            _draws = 0;
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

        // With the single golden of an ordinary level off (OrdinaryGoldenEveryLevels 0, as the fixture leaves it).
        [Test]
        public void Golden_NormalLevel_NeverDue()
        {
            _level.IsExtraColorLevel.Returns(false);
            SetCompletion(0.95f);

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void OrdinaryGolden_ACheckLevelWinsItsRoll_OneComesAtTheThreshold()
        {
            UseOrdinaryGolden(level: 10, roll: 0.3f);
            SetCompletion(0.7f);
            Assert.IsFalse(_service.TryTakeDue(out _), "below the threshold");

            SetCompletion(0.8f);
            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        [Test]
        public void OrdinaryGolden_ACheckLevelLosesItsRoll_NoneThisLevel()
        {
            UseOrdinaryGolden(level: 5, roll: 0.5f);
            SetCompletion(0.8f);

            Assert.IsFalse(LinesUntilDue(20));
        }

        // One roll a level: a lost roll on the first line isn't tried again on the next.
        [Test]
        public void OrdinaryGolden_RollsOnceALevel()
        {
            UseOrdinaryGolden(level: 5, roll: 0.5f);
            _service.TryTakeDue(out _);

            _roll = 0f;
            SetCompletion(0.8f);

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void OrdinaryGolden_NotACheckLevel_NeverRolls()
        {
            UseOrdinaryGolden(level: 7, roll: 0f);
            SetCompletion(0.95f);

            Assert.IsFalse(LinesUntilDue(20));
            Assert.AreEqual(0, _draws);
        }

        // Off a purple level the golden is a single one: once it has come and gone, no other follows.
        [Test]
        public void OrdinaryGolden_IsASingleOne()
        {
            UseOrdinaryGolden(level: 5, roll: 0f);
            SetCompletion(0.8f);
            Assert.IsTrue(_service.TryTakeDue(out _));
            _service.Placed(BalloonType.Golden)();

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void OrdinaryGolden_TheNextCheckLevelRollsAgain()
        {
            UseOrdinaryGolden(level: 5, roll: 0.5f);
            SetCompletion(0.8f);
            Assert.IsFalse(_service.TryTakeDue(out _));

            _levelNumber.Value = 10;
            _roll = 0.3f;

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        // The sure level is the player's first golden balloon: no roll, so even the worst roll brings it.
        [Test]
        public void SureGolden_ItsLevelAlwaysBringsOne_WithoutARoll()
        {
            UseOrdinaryGolden(level: 5, roll: 0.99f);
            _settings.SureGoldenLevel.Returns(5);
            SetCompletion(0.8f);

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
            Assert.AreEqual(0, _draws);
        }

        // The player's first golden balloon comes early, with the level still ahead of them.
        [Test]
        public void SureGolden_ComesEarly_AtItsOwnCompletion()
        {
            UseOrdinaryGolden(level: 5, roll: 0.99f);
            _settings.SureGoldenLevel.Returns(5);
            _settings.SureGoldenAtCompletion.Returns(0.1f);
            SetCompletion(0.05f);

            Assert.IsFalse(_service.TryTakeDue(out _));

            SetCompletion(0.12f);

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        // The early completion is the sure level's alone: a rolled one still waits for the usual completion.
        [Test]
        public void ARolledGolden_StillWaitsForTheUsualCompletion()
        {
            UseOrdinaryGolden(level: 10, roll: 0f);
            _settings.SureGoldenLevel.Returns(5);
            _settings.SureGoldenAtCompletion.Returns(0.1f);
            SetCompletion(0.12f);

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void SureGolden_OtherCheckLevelsStillRoll()
        {
            UseOrdinaryGolden(level: 10, roll: 0.99f);
            _settings.SureGoldenLevel.Returns(5);
            SetCompletion(0.8f);

            Assert.IsFalse(LinesUntilDue(20));
        }

        [Test]
        public void SureGolden_IsASingleOne()
        {
            UseOrdinaryGolden(level: 5, roll: 0.99f);
            _settings.SureGoldenLevel.Returns(5);
            SetCompletion(0.8f);
            Assert.IsTrue(_service.TryTakeDue(out _));
            _service.Placed(BalloonType.Golden)();

            Assert.IsFalse(LinesUntilDue(20));
        }

        [TestCase(5, 5, true)]
        [TestCase(5, 10, true)]
        [TestCase(5, 4, false)]
        [TestCase(5, 6, false)]
        [TestCase(0, 5, false)]
        [TestCase(5, 0, false)]
        public void IsOrdinaryGoldenLevel_EveryNthLevel(int every, int level, bool expected)
        {
            _settings.OrdinaryGoldenEveryLevels.Returns(every);

            Assert.AreEqual(expected, RewardBalloonService.IsOrdinaryGoldenLevel(_settings, level));
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
        public void PurpleGolden_BelowItsCompletion_NeitherRollsNorCounts()
        {
            UsePurple(GoldenChances, Empty);
            _roll = 0f;
            SetCompletion(0.74f);

            Assert.IsFalse(LinesUntilDue(6));
            Assert.AreEqual(0, _draws);

            SetCompletion(0.8f);

            Assert.AreEqual(1, FirstDueLine(BalloonType.Golden, 5), "the first line at its completion rolls");
        }

        [TestCase(0.34f, true)]
        [TestCase(0.35f, false)]
        public void PurpleGolden_FirstLineAtItsCompletion_RollsAgainstTheFirstChance(float roll, bool due)
        {
            UsePurple(GoldenChances, Empty);
            _roll = roll;
            SetCompletion(0.8f);

            Assert.AreEqual(due, _service.TryTakeDue(out var type));
            if (due)
            {
                Assert.AreEqual(BalloonType.Golden, type);
            }
        }

        [Test]
        public void PurpleGolden_LostRoll_RollsAgainEveryNthLine()
        {
            UsePurple(GoldenChances, Empty);
            _roll = 1f;
            SetCompletion(0.8f);
            var expectedDraws = new[] { 1, 1, 1, 2, 2, 2, 3 };

            for (var line = 0; line < expectedDraws.Length; line++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _), $"line {line + 1}");
                Assert.AreEqual(expectedDraws[line], _draws, $"draws after line {line + 1}");
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void PurpleGolden_EachRoll_JustUnderItsEntryOfTheList_Wins(int placed)
        {
            UsePurple(GoldenChances, Empty);
            PlaceGoldens(placed);
            SkipToTheNextRoll(placed);
            _roll = GoldenChances[placed] - 0.001f;

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Golden, type);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void PurpleGolden_EachRoll_AtItsEntryOfTheList_Loses(int placed)
        {
            UsePurple(GoldenChances, Empty);
            PlaceGoldens(placed);
            SkipToTheNextRoll(placed);
            _roll = GoldenChances[placed];

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void PurpleGolden_ThreeAtMost_ACertainRollForAFourthBringsNone()
        {
            UsePurple(GoldenChances, Empty);
            PlaceGoldens(3);
            _roll = 0f;

            Assert.IsFalse(LinesUntilDue(30));
        }

        [Test]
        public void PurpleGolden_WonRoll_StaysDueEveryLineWithoutRollingAgain()
        {
            UsePurple(GoldenChances, Empty);
            _roll = 0f;
            SetCompletion(0.8f);
            Assert.IsTrue(_service.TryTakeDue(out _));

            _roll = 1f;
            for (var line = 0; line < 6; line++)
            {
                Assert.IsTrue(_service.TryTakeDue(out var type), $"line {line + 1} after the win");
                Assert.AreEqual(BalloonType.Golden, type);
            }

            Assert.AreEqual(1, _draws);
        }

        [Test]
        public void PurpleGolden_WhileOneIsOnTheBoard_LinesDontCountOrRoll()
        {
            UsePurple(GoldenChances, Empty);
            _roll = 0f;
            SetCompletion(0.8f);
            Assert.IsTrue(_service.TryTakeDue(out _));
            var left = _service.Placed(BalloonType.Golden);

            Assert.IsFalse(LinesUntilDue(10));
            Assert.AreEqual(1, _draws, "only the roll that won");
            left();

            Assert.AreEqual(RollEvery, FirstDueLine(BalloonType.Golden, 10), "the count starts over once it's gone");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PurpleGolden_NewLevelOrRunReset_ForgetsWhatWasOwed(bool runReset)
        {
            UsePurple(GoldenChances, Empty);
            _roll = 0f;
            SetCompletion(0.8f);
            Assert.IsTrue(_service.TryTakeDue(out _));

            StartOver(runReset);
            SetCompletion(0.5f);

            Assert.IsFalse(LinesUntilDue(10));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PurpleGolden_NewLevelOrRunReset_CountsPlacedStartOverAndTheFirstLineRolls(bool runReset)
        {
            UsePurple(GoldenChances, Empty);
            PlaceGoldens(1);

            StartOver(runReset);
            _roll = GoldenChances[0] - 0.001f;

            Assert.IsTrue(_service.TryTakeDue(out _), "the first chance, on the first line, not the second's");
        }

        [TestCase(0.39f, true)]
        [TestCase(0.4f, false)]
        public void PurpleHeart_FirstLineAtFullHearts_RollsAgainstTheFirstChance(float roll, bool due)
        {
            UsePurple(Empty, HeartChances);
            _roll = roll;

            Assert.AreEqual(due, _service.TryTakeDue(out var type));
            if (due)
            {
                Assert.AreEqual(BalloonType.Heart, type);
            }
        }

        [Test]
        public void PurpleHeart_LostRoll_RollsAgainEveryNthLine()
        {
            UsePurple(Empty, HeartChances);
            _roll = 1f;
            var expectedDraws = new[] { 1, 1, 1, 2, 2, 2, 3 };

            for (var line = 0; line < expectedDraws.Length; line++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _), $"line {line + 1}");
                Assert.AreEqual(expectedDraws[line], _draws, $"draws after line {line + 1}");
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void PurpleHeart_EachRoll_JustUnderItsEntryOfTheList_Wins(int placed)
        {
            UsePurple(Empty, HeartChances);
            PlaceHearts(placed);
            SkipToTheNextRoll(placed);
            _roll = HeartChances[placed] - 0.001f;

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void PurpleHeart_EachRoll_AtItsEntryOfTheList_Loses(int placed)
        {
            UsePurple(Empty, HeartChances);
            PlaceHearts(placed);
            SkipToTheNextRoll(placed);
            _roll = HeartChances[placed];

            Assert.IsFalse(_service.TryTakeDue(out _));
        }

        [Test]
        public void PurpleHeart_FiveAtMost_ACertainRollForASixthBringsNone()
        {
            UsePurple(Empty, HeartChances);
            PlaceHearts(5);
            _roll = 0f;

            Assert.IsFalse(HeartWithin(30));
        }

        [Test]
        public void PurpleHeart_ACertainChance_WinsWithoutADraw()
        {
            UsePurple(Empty, new[] { 1f });
            _roll = 1f;

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Heart, type);
            Assert.AreEqual(0, _draws);
        }

        [Test]
        public void PurpleHeart_WonRoll_StaysDueEveryLineWithoutRollingAgain()
        {
            UsePurple(Empty, HeartChances);
            _roll = 0f;
            Assert.IsTrue(_service.TryTakeDue(out _));

            _roll = 1f;
            for (var line = 0; line < 6; line++)
            {
                Assert.IsTrue(_service.TryTakeDue(out var type), $"line {line + 1} after the win");
                Assert.AreEqual(BalloonType.Heart, type);
            }

            Assert.AreEqual(1, _draws);
        }

        [Test]
        public void PurpleHeart_WhileOneIsOnTheBoard_LinesDontCountOrRoll()
        {
            UsePurple(Empty, HeartChances);
            _roll = 0f;
            Assert.IsTrue(_service.TryTakeDue(out _));
            var left = _service.Placed(BalloonType.Heart);

            Assert.IsFalse(HeartWithin(10));
            Assert.AreEqual(1, _draws, "only the roll that won");
            left();

            Assert.AreEqual(RollEvery, FirstDueLine(BalloonType.Heart, 10), "the count starts over once it's gone");
        }

        [Test]
        public void PurpleHeart_HurtAndTheCooldown_DoNotApplyOnPurple()
        {
            UsePurple(Empty, HeartChances);
            _hearts.Value = 1;
            PlaceHearts(1);

            Assert.AreEqual(RollEvery, FirstDueLine(BalloonType.Heart, 10), "sooner than the off-purple cooldown");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PurpleHeart_NewLevelOrRunReset_ForgetsWhatWasOwedAndTheFirstLineRolls(bool runReset)
        {
            UsePurple(Empty, HeartChances);
            _roll = 0f;
            Assert.IsTrue(_service.TryTakeDue(out _));

            StartOver(runReset);
            _roll = 1f;

            Assert.IsFalse(_service.TryTakeDue(out _), "what the last level won is gone");
            Assert.AreEqual(2, _draws, "and this line rolled afresh");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PurpleHeart_NewLevelOrRunReset_CountsPlacedStartOver(bool runReset)
        {
            UsePurple(Empty, HeartChances);
            PlaceHearts(1);

            StartOver(runReset);
            _roll = HeartChances[0] - 0.001f;

            Assert.IsTrue(_service.TryTakeDue(out _), "the first chance, on the first line, not the second's");
        }

        [Test]
        public void PurpleRolls_TheGoldenTakesTheLine_TheHeartRollsOnTheNextAndWasNotCounted()
        {
            UsePurple(GoldenChances, HeartChances);
            _roll = 0f;
            SetCompletion(0.8f);

            Assert.IsTrue(_service.TryTakeDue(out var first));
            Assert.AreEqual(BalloonType.Golden, first);
            Assert.AreEqual(1, _draws, "the golden's roll alone");
            _service.Placed(BalloonType.Golden);

            Assert.IsTrue(_service.TryTakeDue(out var second), "the heart's first roll, still on its first line");
            Assert.AreEqual(BalloonType.Heart, second);
        }

        [Test]
        public void PurpleRolls_OneOfEachOnTheBoard_NothingMoreIsDue()
        {
            UsePurple(GoldenChances, HeartChances);
            _roll = 0f;
            SetCompletion(0.8f);

            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Golden);
            _service.TryTakeDue(out _);
            _service.Placed(BalloonType.Heart);

            Assert.IsFalse(_service.TryTakeDue(out _), "one of each on the board, nothing more");
        }

        [Test]
        public void PurpleRolls_NoGoldenEntry_TheHeartStillRolls()
        {
            UsePurple(GoldenChances, HeartChances);
            _service.Dispose();
            _service = CreateService(BalloonType.Heart);
            _roll = 0f;
            SetCompletion(0.9f);

            Assert.IsTrue(_service.TryTakeDue(out var type));
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PurpleRolls_NoListsConfigured_NothingIsDueAndNothingThrows(bool nullLists)
        {
            _roll = 0f;
            UsePurple(nullLists ? null : Empty, nullLists ? null : Empty);
            SetCompletion(1f);

            Assert.DoesNotThrow(() => Assert.IsFalse(LinesUntilDue(10) || HeartWithin(10)));
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
        public void Heart_InNeed_EachLineRolledWithoutOne_RaisesTheNextLinesChance()
        {
            // The last heart: 12% from the settings, and each miss at full need adds half of that.
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = 1;
            _need.Need.Returns(1f);
            _roll = 0.2f;

            Assert.IsFalse(_service.TryTakeDue(out _), "12%");
            Assert.IsFalse(_service.TryTakeDue(out _), "18%");
            Assert.IsTrue(_service.TryTakeDue(out var type), "24%");
            Assert.AreEqual(BalloonType.Heart, type);
        }

        [Test]
        public void Heart_OutOfNeed_LinesWithoutOne_KeepTheChance()
        {
            _level.IsExtraColorLevel.Returns(false);
            _hearts.Value = 1;
            _roll = 0.2f;

            for (var i = 0; i < 10; i++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _));
            }
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

        private static BalloonPrefabEntry Entry(BalloonType type)
        {
            var entry = new BalloonPrefabEntry();
            typeof(BalloonPrefabEntry).GetField("_balloonType", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(entry, type);
            return entry;
        }

        // The purple rolls on, every RollEvery lines. The service is reset so its counts are primed from these
        // settings, as a level's start does.
        private void UsePurple(IReadOnlyList<float> goldens, IReadOnlyList<float> hearts)
        {
            _settings.PurpleRollEveryLines.Returns(RollEvery);
            _settings.PurpleGoldenChances.Returns(goldens);
            _settings.PurpleHeartChances.Returns(hearts);
            _service.ResetRun(1);
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

            var service = new RewardBalloonService(balloons, _levelProgress, _levelParams, _health, _need, _assist)
            {
                Random01 = () =>
                {
                    _draws++;
                    return _roll;
                },
            };
            service.Start();
            return service;
        }

        private void StartOver(bool runReset)
        {
            if (runReset)
            {
                _service.ResetRun(1);
            }
            else
            {
                _levelNumber.Value++;
            }
        }

        // The purple level's next golden balloons, each won by a certain roll, placed and gone again.
        private void PlaceGoldens(int count)
        {
            SetCompletion(0.8f);
            for (var i = 0; i < count; i++)
            {
                _roll = 0f;
                Assert.Greater(FirstDueLine(BalloonType.Golden, 10), 0, $"golden {i + 1}");
                _service.Placed(BalloonType.Golden)();
            }
        }

        private void PlaceHearts(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _roll = 0f;
                Assert.Greater(FirstDueLine(BalloonType.Heart, 10), 0, $"heart {i + 1}");
                _service.Placed(BalloonType.Heart)();
            }
        }

        // Up to the line of the next roll: the first of a level is on its first line, the ones after a placement
        // come RollEvery lines on.
        private void SkipToTheNextRoll(int placed)
        {
            if (placed == 0)
            {
                return;
            }

            for (var line = 1; line < RollEvery; line++)
            {
                Assert.IsFalse(_service.TryTakeDue(out _), $"line {line}, before the roll");
            }
        }

        // The 1-based line a balloon of the type falls due on, within the given number of lines; 0 for none.
        private int FirstDueLine(BalloonType wanted, int lines)
        {
            for (var line = 1; line <= lines; line++)
            {
                if (_service.TryTakeDue(out var type) && type == wanted)
                {
                    return line;
                }
            }

            return 0;
        }

        // Whether a heart balloon falls due within the given number of lines.
        private bool HeartWithin(int lines)
        {
            return FirstDueLine(BalloonType.Heart, lines) > 0;
        }

        // Whether a golden balloon falls due within the given number of lines, stopping at the first that does.
        private bool LinesUntilDue(int lines)
        {
            return FirstDueLine(BalloonType.Golden, lines) > 0;
        }

        // A non-purple level that is a check level for the single golden balloon (every 5th, a 40% chance),
        // with the roll it will make.
        private void UseOrdinaryGolden(int level, float roll)
        {
            _settings.OrdinaryGoldenEveryLevels.Returns(5);
            _settings.OrdinaryGoldenChance.Returns(0.4f);
            _level.IsExtraColorLevel.Returns(false);
            _levelNumber.Value = level;
            _roll = roll;
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
