#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Cheats
{
    internal class TriggerLevelUpCheat : ICheat
    {
        private readonly IGameConfiguration _config;
        private readonly IGamePalette _palette;
        private readonly IPublisher<ActorHitMessage> _hitPublisher;
        private readonly ScoreController _scoreController;

        public string Name => "Trigger Level Up";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "levelup" };

        public TriggerLevelUpCheat(
            IGameConfiguration config,
            IGamePalette palette,
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
            var required = _config.PointsRequiredForLevel(_scoreController.Level.Value + 1);
            foreach (var color in _palette.Colors)
            {
                ScoreCheatHelper.FillColor(color, required, _scoreController, _hitPublisher);
            }
        }
    }
}
#endif
