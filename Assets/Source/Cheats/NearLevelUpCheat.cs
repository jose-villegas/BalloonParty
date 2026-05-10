#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using BalloonParty.Game;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Cheats
{
    public class NearLevelUpCheat : ICheat
    {
        public string Name => "Near Level Up";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "levelup" };
        private readonly IGameConfiguration _config;
        private readonly IPublisher<BalloonHitMessage> _hitPublisher;
        private readonly ScoreController _scoreController;

        public NearLevelUpCheat(
            IGameConfiguration config,
            ScoreController scoreController,
            IPublisher<BalloonHitMessage> hitPublisher)
        {
            _config = config;
            _scoreController = scoreController;
            _hitPublisher = hitPublisher;
        }

        public void Execute()
        {
            var oneBeforeRequired = _config.PointsRequiredForLevel(_scoreController.Level.Value + 1) - 1;
            foreach (var color in _config.BalloonColors)
            {
                ScoreCheatHelper.FillColor(color, oneBeforeRequired, _scoreController, _hitPublisher);
            }
        }
    }
}
#endif
