#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Cheats
{
    internal class NearLevelUpCheat : ICheat
    {
        private readonly IGameConfiguration _config;
        private readonly GamePalette _palette;
        private readonly IPublisher<ActorHitMessage> _hitPublisher;
        private readonly ScoreController _scoreController;

        public string Name => "Near Level Up";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "levelup" };

        public NearLevelUpCheat(
            IGameConfiguration config,
            GamePalette palette,
            ScoreController scoreController,
            IPublisher<ActorHitMessage> hitPublisher)
        {
            _config = config;
            _palette = palette;
            _scoreController = scoreController;
            _hitPublisher = hitPublisher;
        }

        public void Execute()
        {
            var oneBeforeRequired = _config.PointsRequiredForLevel(_scoreController.Level.Value + 1) - 3;
            foreach (var color in _palette.Colors)
            {
                ScoreCheatHelper.FillColor(color, oneBeforeRequired, _scoreController, _hitPublisher);
            }
        }
    }
}
#endif
