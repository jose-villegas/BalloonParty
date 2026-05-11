#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Game;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Cheats
{
    public class TriggerLevelUpCheat : ICheat
    {
        private readonly IGameConfiguration _config;
        private readonly IPublisher<BalloonHitMessage> _hitPublisher;
        private readonly ScoreController _scoreController;

        public string Name => "Trigger Level Up";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "levelup" };

        public TriggerLevelUpCheat(
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
            var required = _config.PointsRequiredForLevel(_scoreController.Level.Value + 1);
            foreach (var color in _config.BalloonColors)
            {
                ScoreCheatHelper.FillColor(color, required, _scoreController, _hitPublisher);
            }
        }
    }
}
#endif
