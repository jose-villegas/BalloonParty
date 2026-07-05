#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;

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
            foreach (var color in _palette.Colors)
            {
                ScoreCheatHelper.FillColor(color, oneBeforeRequired, _scoreController, _hitDispatcher);
            }
        }
    }
}
#endif
