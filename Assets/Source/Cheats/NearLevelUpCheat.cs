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
        private readonly IActiveLevelParameters _levelParams;
        private readonly IGamePalette _palette;
        private readonly IHitDispatcher _hitDispatcher;
        private readonly ScoreController _scoreController;

        public string Name => "Near Level Up";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "levelup" };

        public NearLevelUpCheat(
            IActiveLevelParameters levelParams,
            IGamePalette palette,
            ScoreController scoreController,
            IHitDispatcher hitDispatcher)
        {
            _levelParams = levelParams;
            _palette = palette;
            _scoreController = scoreController;
            _hitDispatcher = hitDispatcher;
        }

        public void Execute()
        {
            var oneBeforeRequired = _levelParams.PointsRequiredForLevel(_scoreController.Level.Value + 1) - 3;
            foreach (var colorName in _levelParams.Current.AllowedColors)
            {
                ScoreCheatHelper.FillColor(_palette.GetEntry(colorName), oneBeforeRequired, _scoreController, _hitDispatcher);
            }
        }
    }
}
#endif
