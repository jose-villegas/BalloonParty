using BalloonParty.Game.Run;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class RunMetaTests
    {
        private const string BestLevelKey = "BestLevel";
        private const string BestScoreKey = "BestScore";

        [SetUp]
        public void SetUp()
        {
            ClearPrefs();
        }

        [TearDown]
        public void TearDown()
        {
            ClearPrefs();
        }

        [Test]
        public void RecordRun_HigherLevel_UpdatesBestLevel()
        {
            var meta = new RunMeta();

            meta.RecordRun(5, 100);

            Assert.AreEqual(5, meta.BestLevel.Value);
        }

        [Test]
        public void RecordRun_LowerLevel_KeepsBestLevel()
        {
            var meta = new RunMeta();
            meta.RecordRun(5, 100);

            meta.RecordRun(3, 40);

            Assert.AreEqual(5, meta.BestLevel.Value);
        }

        [Test]
        public void RecordRun_TracksLevelAndScoreIndependently()
        {
            var meta = new RunMeta();
            meta.RecordRun(5, 10);

            // A shorter run with a higher score still raises best score, not best level.
            meta.RecordRun(2, 80);

            Assert.AreEqual(5, meta.BestLevel.Value);
            Assert.AreEqual(80, meta.BestScore.Value);
        }

        [Test]
        public void RecordRun_PersistsAcrossInstances()
        {
            new RunMeta().RecordRun(7, 250);

            var reloaded = new RunMeta();

            Assert.AreEqual(7, reloaded.BestLevel.Value);
            Assert.AreEqual(250, reloaded.BestScore.Value);
        }

        [Test]
        public void Construct_NoPrefs_DefaultsToZero()
        {
            var meta = new RunMeta();

            Assert.AreEqual(0, meta.BestLevel.Value);
            Assert.AreEqual(0, meta.BestScore.Value);
        }

        private static void ClearPrefs()
        {
            PlayerPrefs.DeleteKey(BestLevelKey);
            PlayerPrefs.DeleteKey(BestScoreKey);
            PlayerPrefs.Save();
        }
    }
}
