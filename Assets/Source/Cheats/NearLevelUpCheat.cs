#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Cheats
{
    internal class NearLevelUpCheat : ICheat
    {
        private readonly ILevelThresholds _levelThresholds;
        private readonly IActiveLevelParameters _levelParams;
        private readonly IGamePalette _palette;
        private readonly IHitDispatcher _hitDispatcher;
        private readonly ILevelProgress _levelProgress;
        private readonly ColorStreakTracker _streak;

        public string Name => "Near Level Up";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "levelup" };

        public NearLevelUpCheat(
            ILevelThresholds levelThresholds,
            IActiveLevelParameters levelParams,
            IGamePalette palette,
            ILevelProgress levelProgress,
            IHitDispatcher hitDispatcher,
            ColorStreakTracker streak)
        {
            _levelThresholds = levelThresholds;
            _levelParams = levelParams;
            _palette = palette;
            _levelProgress = levelProgress;
            _hitDispatcher = hitDispatcher;
            _streak = streak;
        }

        public void Execute()
        {
            var oneBeforeRequired = _levelThresholds.PointsRequiredForLevel(_levelProgress.Level.Value) - 3;
            foreach (var colorName in _levelParams.Current.AllowedColors)
            {
                ScoreCheatHelper.FillColor(
                    _palette.GetEntry(colorName), oneBeforeRequired, _levelProgress, _hitDispatcher, _streak);
            }
        }
    }
}
#endif
