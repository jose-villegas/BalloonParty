using UniRx;
using UnityEngine;

namespace BalloonParty.Game.Run
{
    internal class RunMeta : IRunMeta
    {
        private const string BestLevelKey = "BestLevel";
        private const string BestScoreKey = "BestScore";

        private readonly ReactiveProperty<int> _bestLevel;
        private readonly ReactiveProperty<int> _bestScore;

        public RunMeta()
        {
            _bestLevel = new ReactiveProperty<int>(PlayerPrefs.GetInt(BestLevelKey, 0));
            _bestScore = new ReactiveProperty<int>(PlayerPrefs.GetInt(BestScoreKey, 0));
        }

        public IReadOnlyReactiveProperty<int> BestLevel => _bestLevel;
        public IReadOnlyReactiveProperty<int> BestScore => _bestScore;

        public void RecordRun(int level, int score)
        {
            var changed = false;

            if (level > _bestLevel.Value)
            {
                _bestLevel.Value = level;
                PlayerPrefs.SetInt(BestLevelKey, level);
                changed = true;
            }

            if (score > _bestScore.Value)
            {
                _bestScore.Value = score;
                PlayerPrefs.SetInt(BestScoreKey, score);
                changed = true;
            }

            if (changed)
            {
                PlayerPrefs.Save();
            }
        }
    }
}
