using BalloonParty.Game.Run;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class RunSnapshotTests
    {
        [Test]
        public void ToJson_ThenTryParse_KeepsEveryField()
        {
            var saved = new RunSnapshot
            {
                Level = 7,
                Seed = -1234567,
                LevelStartScore = 980,
                Hearts = 4,
                RetriesUsed = 2,
                OnGameOver = true,
                FinalScore = 1310,
                FreeCardsDealt = 1,
                BonusCard = "Shield"
            };
            saved.Cards.Add(new HeldCardRecord("Faster Reload", 2));

            Assert.IsTrue(RunSnapshot.TryParse(saved.ToJson(), out var loaded));

            Assert.AreEqual(7, loaded.Level);
            Assert.AreEqual(-1234567, loaded.Seed);
            Assert.AreEqual(980, loaded.LevelStartScore);
            Assert.AreEqual(4, loaded.Hearts);
            Assert.AreEqual(2, loaded.RetriesUsed);
            Assert.IsTrue(loaded.OnGameOver);
            Assert.AreEqual(1310, loaded.FinalScore);
            Assert.AreEqual(1, loaded.FreeCardsDealt);
            Assert.AreEqual("Shield", loaded.BonusCard);
            Assert.AreEqual(1, loaded.Cards.Count);
            Assert.AreEqual("Faster Reload", loaded.Cards[0].Title);
            Assert.AreEqual(2, loaded.Cards[0].Stacks);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json at all")]
        [TestCase("{\"Version\":1,\"Level\":")]
        public void TryParse_ACorruptSave_IsDiscarded(string json)
        {
            Assert.IsFalse(RunSnapshot.TryParse(json, out var loaded));
            Assert.IsNull(loaded);
        }

        [Test]
        public void TryParse_ASaveOfAnotherVersion_IsDiscarded()
        {
            var json = new RunSnapshot { Level = 3, Hearts = 2 }.ToJson()
                .Replace($"\"Version\":{RunSnapshot.CurrentVersion}", "\"Version\":0");

            Assert.IsFalse(RunSnapshot.TryParse(json, out _));
        }

        [TestCase(0, 2, false, 0, TestName = "no level")]
        [TestCase(3, 0, false, 0, TestName = "a level begun with no hearts")]
        [TestCase(3, -1, true, 50, TestName = "negative hearts")]
        [TestCase(3, 0, true, 10, TestName = "a final score below the level's start")]
        public void TryParse_ImpossibleValues_AreDiscarded(int level, int hearts, bool onGameOver, int finalScore)
        {
            var json = new RunSnapshot
            {
                Level = level,
                Hearts = hearts,
                OnGameOver = onGameOver,
                LevelStartScore = 20,
                FinalScore = finalScore
            }.ToJson();

            Assert.IsFalse(RunSnapshot.TryParse(json, out _));
        }

        [Test]
        public void TryParse_TheGameOverScreen_MayHaveNoHearts()
        {
            var json = new RunSnapshot { Level = 3, Hearts = 0, OnGameOver = true, FinalScore = 40 }.ToJson();

            Assert.IsTrue(RunSnapshot.TryParse(json, out _));
        }
    }
}
