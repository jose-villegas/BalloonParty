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
    internal class TriggerLevelUpCheat : ICheat
    {
        private readonly ILevelThresholds _levelThresholds;
        private readonly IActiveLevelParameters _levelParams;
        private readonly IGamePalette _palette;
        private readonly IHitDispatcher _hitDispatcher;
        private readonly ILevelProgress _levelProgress;

        public string Name => "Trigger Level Up";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "levelup" };

        public TriggerLevelUpCheat(
            ILevelThresholds levelThresholds,
            IActiveLevelParameters levelParams,
            IGamePalette palette,
            ILevelProgress levelProgress,
            IHitDispatcher hitDispatcher)
        {
            _levelThresholds = levelThresholds;
            _levelParams = levelParams;
            _palette = palette;
            _levelProgress = levelProgress;
            _hitDispatcher = hitDispatcher;
        }

        public void Execute()
        {
            var required = _levelThresholds.PointsRequiredForLevel(_levelProgress.Level.Value + 1);
            foreach (var colorName in _levelParams.Current.AllowedColors)
            {
                ScoreCheatHelper.FillColor(_palette.GetEntry(colorName), required, _levelProgress, _hitDispatcher);
            }
        }
    }
}
#endif
