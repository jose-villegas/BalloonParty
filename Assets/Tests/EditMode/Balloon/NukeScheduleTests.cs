using BalloonParty.Balloon.Reward;
using BalloonParty.Configuration.Balloons;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class NukeScheduleTests
    {
        private const int FromLevel = 3;
        private const int Cooldown = 4;
        private const int SureLevel = 10;
        private const int SureAfter = 3;

        private INukeOddsSettings _settings;
        private INukeOdds _odds;
        private NukeSchedule _schedule;
        private float _roll;
        private int _draws;

        [SetUp]
        public void SetUp()
        {
            _settings = Substitute.For<INukeOddsSettings>();
            _settings.FromLevel.Returns(FromLevel);
            _settings.PerLevel.Returns(5);
            _settings.CooldownLines.Returns(Cooldown);
            _odds = Substitute.For<INukeOdds>();
            _odds.Chance.Returns(1f);
            _roll = 1f;
            _draws = 0;
            _schedule = new NukeSchedule(_settings, _odds, () =>
            {
                _draws++;
                return _roll;
            });
        }

        [Test]
        public void Due_BelowFromLevel_NeverRollsAndDrawsNothing()
        {
            for (var line = 0; line < 10; line++)
            {
                Assert.IsFalse(_schedule.Due(FromLevel - 1), $"line {line + 1}");
            }

            Assert.AreEqual(0, _draws);
            _ = _odds.DidNotReceive().Chance;
        }

        [Test]
        public void Due_AtFromLevel_Rolls()
        {
            Assert.IsTrue(_schedule.Due(FromLevel));
        }

        [Test]
        public void Due_ChanceOne_WinsWithoutADraw()
        {
            Assert.IsTrue(_schedule.Due(FromLevel));
            Assert.AreEqual(0, _draws);
        }

        [Test]
        public void Due_ChanceZero_NeverWinsAndNeverDraws()
        {
            _odds.Chance.Returns(0f);
            _roll = 0f;

            for (var line = 0; line < 20; line++)
            {
                Assert.IsFalse(_schedule.Due(FromLevel), $"line {line + 1}");
            }

            Assert.AreEqual(0, _draws);
        }

        [TestCase(0.29f, true)]
        [TestCase(0.3f, false)]
        public void Due_PartialChance_WinsOnlyUnderTheDraw(float roll, bool due)
        {
            _odds.Chance.Returns(0.3f);
            _roll = roll;

            Assert.AreEqual(due, _schedule.Due(FromLevel));
            Assert.AreEqual(1, _draws);
        }

        [Test]
        public void Due_ALostRoll_RollsAgainOnTheNextLine()
        {
            _odds.Chance.Returns(0.3f);
            _roll = 0.9f;
            Assert.IsFalse(_schedule.Due(FromLevel));

            _roll = 0.1f;

            Assert.IsTrue(_schedule.Due(FromLevel));
            Assert.AreEqual(2, _draws);
        }

        [Test]
        public void Due_AWonRoll_StaysOwedAcrossLinesWithoutRollingAgain()
        {
            _odds.Chance.Returns(0.3f);
            _roll = 0f;
            Assert.IsTrue(_schedule.Due(FromLevel));

            _roll = 1f;
            for (var line = 0; line < 6; line++)
            {
                Assert.IsTrue(_schedule.Due(FromLevel), $"line {line + 1} after the win");
            }

            Assert.AreEqual(1, _draws);
        }

        [Test]
        public void Due_WhileOneIsOnTheBoard_False()
        {
            _schedule.Due(FromLevel);
            _schedule.Placed();

            for (var line = 0; line < Cooldown * 3; line++)
            {
                Assert.IsFalse(_schedule.Due(FromLevel), $"line {line + 1}");
            }
        }

        [Test]
        public void Placed_LeaveCallback_FreesItAfterTheCooldown()
        {
            _schedule.Due(FromLevel);
            _schedule.Placed()();

            for (var line = 1; line < Cooldown; line++)
            {
                Assert.IsFalse(_schedule.Due(FromLevel), $"line {line} of the cooldown");
            }

            Assert.IsTrue(_schedule.Due(FromLevel));
        }

        // The cooldown counts from the placement, so the lines the nuke spent on the board are part of it.
        [Test]
        public void Cooldown_CountsFromPlacement_NotFromTheLeave()
        {
            _schedule.Due(FromLevel);
            var left = _schedule.Placed();
            _schedule.Due(FromLevel);
            _schedule.Due(FromLevel);
            left();

            Assert.IsFalse(_schedule.Due(FromLevel), "the third line since placement");
            Assert.IsTrue(_schedule.Due(FromLevel), "the fourth");
        }

        [Test]
        public void Due_AfterARunReset_AStaleLeaveCallback_DoesNotFreeTheNukeOnTheBoard()
        {
            _schedule.Due(FromLevel);
            var staleLeft = _schedule.Placed();

            _schedule.ResetRun();
            Assert.IsTrue(_schedule.Due(FromLevel));
            _schedule.Placed();
            staleLeft();

            for (var line = 0; line < Cooldown * 3; line++)
            {
                Assert.IsFalse(_schedule.Due(FromLevel), $"line {line + 1}, the new nuke is still up");
            }
        }

        [Test]
        public void Due_AnEarlierNukesLeaveCallback_DoesNotFreeTheOneOnTheBoard()
        {
            _schedule.Due(FromLevel);
            var firstLeft = _schedule.Placed();
            firstLeft();
            for (var line = 0; line < Cooldown; line++)
            {
                _schedule.Due(FromLevel);
            }

            _schedule.Placed();
            firstLeft();

            for (var line = 0; line < Cooldown * 3; line++)
            {
                Assert.IsFalse(_schedule.Due(FromLevel), $"line {line + 1}, the second nuke is still up");
            }
        }

        [Test]
        public void PerLevel_CapsTheNukesOfALevel_AndForgetLevelResetsIt()
        {
            _settings.PerLevel.Returns(1);
            _settings.CooldownLines.Returns(0);
            Assert.IsTrue(_schedule.Due(FromLevel));
            _schedule.Placed()();

            for (var line = 0; line < 5; line++)
            {
                Assert.IsFalse(_schedule.Due(FromLevel), $"line {line + 1}, the level's one is placed");
            }

            _schedule.ForgetLevel();

            Assert.IsTrue(_schedule.Due(FromLevel));
        }

        [Test]
        public void PerLevel_Two_AllowsASecondAfterTheCooldown()
        {
            _settings.PerLevel.Returns(2);
            _settings.CooldownLines.Returns(0);
            _schedule.Due(FromLevel);
            _schedule.Placed()();
            Assert.IsTrue(_schedule.Due(FromLevel));
            _schedule.Placed()();

            Assert.IsFalse(_schedule.Due(FromLevel));
        }

        [Test]
        public void Cooldown_TheFirstNukeAfterARunReset_WaitsOnNone()
        {
            _schedule.Due(FromLevel);
            _schedule.Placed()();

            _schedule.ResetRun();

            Assert.IsTrue(_schedule.Due(FromLevel));
        }

        [Test]
        public void Cooldown_TheFirstNukeOfARun_WaitsOnNone()
        {
            Assert.IsTrue(_schedule.Due(FromLevel));
        }

        [Test]
        public void ResetRun_DropsAnOwedNukeAndTheOneOnTheBoard()
        {
            _schedule.Due(FromLevel);
            _schedule.Placed();

            _schedule.ResetRun();
            _odds.Chance.Returns(0f);

            Assert.IsFalse(_schedule.Due(FromLevel));
            _odds.Chance.Returns(1f);
            Assert.IsTrue(_schedule.Due(FromLevel), "no longer on the board");
        }

        [Test]
        public void ForgetLevel_DropsANukeStillOwed()
        {
            _odds.Chance.Returns(0.3f);
            _roll = 0f;
            Assert.IsTrue(_schedule.Due(FromLevel));

            _schedule.ForgetLevel();
            _roll = 1f;

            Assert.IsFalse(_schedule.Due(FromLevel), "a fresh roll, lost");
            Assert.AreEqual(2, _draws);
        }

        [Test]
        public void ForgetLevel_LeavesTheOneOnTheBoardAlone()
        {
            _schedule.Due(FromLevel);
            _schedule.Placed();

            _schedule.ForgetLevel();

            Assert.IsFalse(_schedule.Due(FromLevel + 1));
        }

        [Test]
        public void Due_WhileOnTheBoard_DrawsNothing()
        {
            _odds.Chance.Returns(0.3f);
            _roll = 0f;
            _schedule.Due(FromLevel);
            _schedule.Placed();
            var draws = _draws;

            _schedule.Due(FromLevel);

            Assert.AreEqual(draws, _draws);
        }

        [Test]
        public void Due_EveryOutcome_TellsTheOddsALinePassedOnce()
        {
            _odds.Chance.Returns(0.3f);
            _roll = 0.9f;
            _schedule.Due(FromLevel - 1);
            _odds.Received(1).LinePassed();

            _schedule.Due(FromLevel);
            _odds.Received(2).LinePassed();

            _roll = 0f;
            _schedule.Due(FromLevel);
            _odds.Received(3).LinePassed();

            _schedule.Due(FromLevel);
            _odds.Received(4).LinePassed();

            _schedule.Placed();
            _schedule.Due(FromLevel);
            _odds.Received(5).LinePassed();
        }

        [Test]
        public void SureLevel_BringsOneAfterItsLines_EvenWithNoChance()
        {
            UseSureLevel();
            _odds.Chance.Returns(0f);

            for (var line = 1; line < SureAfter; line++)
            {
                Assert.IsFalse(_schedule.Due(SureLevel), $"line {line}, before the sure line");
            }

            Assert.IsTrue(_schedule.Due(SureLevel));
            Assert.AreEqual(0, _draws);
        }

        [Test]
        public void SureLevel_IgnoresTheCooldown()
        {
            UseSureLevel();
            _settings.CooldownLines.Returns(1000);
            _schedule.Due(FromLevel);
            _schedule.Placed()();
            _schedule.ForgetLevel();

            Assert.IsTrue(DueWithin(SureLevel, SureAfter), "the cooldown from the last level's nuke is ignored");
        }

        [Test]
        public void SureLevel_BelowFromLevel_StillBringsOne()
        {
            UseSureLevel();
            _settings.FromLevel.Returns(SureLevel + 5);

            Assert.IsTrue(DueWithin(SureLevel, SureAfter));
        }

        [Test]
        public void SureLevel_CountsItsLinesFromTheLevelsStart()
        {
            UseSureLevel();
            _odds.Chance.Returns(0f);
            for (var line = 0; line < 20; line++)
            {
                _schedule.Due(SureLevel - 1);
            }

            _schedule.ForgetLevel();

            Assert.IsFalse(_schedule.Due(SureLevel), "the last level's lines do not count");
        }

        [Test]
        public void SureLevel_NotWhileOneIsOnTheBoard()
        {
            UseSureLevel();
            _schedule.Due(FromLevel);
            _schedule.Placed();
            _schedule.ForgetLevel();

            Assert.IsFalse(DueWithin(SureLevel, SureAfter * 3));
        }

        [Test]
        public void SureLevel_NotASecondOnceTheLevelHasHadItsOne()
        {
            UseSureLevel();
            _settings.PerLevel.Returns(1);
            Assert.IsTrue(_schedule.Due(SureLevel), "won on the roll before the sure line");
            _schedule.Placed()();

            Assert.IsFalse(DueWithin(SureLevel, SureAfter * 3));
        }

        [Test]
        public void SureLevel_PerLevelZero_TurnsItOff()
        {
            UseSureLevel();
            _settings.PerLevel.Returns(0);

            Assert.IsFalse(DueWithin(SureLevel, SureAfter * 3));
        }

        [Test]
        public void SureLevel_OtherLevels_StayOnTheOdds()
        {
            UseSureLevel();
            _odds.Chance.Returns(0f);

            Assert.IsFalse(DueWithin(SureLevel + 1, SureAfter * 3));
        }

        private void UseSureLevel()
        {
            _settings.SureLevel.Returns(SureLevel);
            _settings.SureAfterLines.Returns(SureAfter);
        }

        private bool DueWithin(int level, int lines)
        {
            for (var line = 0; line < lines; line++)
            {
                if (_schedule.Due(level))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
