#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Game;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;

namespace BalloonParty.Debug
{
    public class NearLevelUpCheat : ICheat
    {
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

        public string Name => "Near Level Up";
        public string Section => "Score";
        public IReadOnlyList<string> Tags => new[] { "score", "levelup" };

        public void Execute()
        {
            var oneBeforeRequired = _config.PointsRequiredForLevel(_scoreController.Level.Value + 1) - 1;
            foreach (var color in _config.BalloonColors)
                FillColor(color, oneBeforeRequired);
        }

        private void FillColor(BalloonColorConfiguration color, int target)
        {
            var missing = target - _scoreController.GetProgress(color.Name);
            if (missing <= 0) return;

            var fakeModel = new BalloonModel();
            fakeModel.Color.Value = color.Name;

            for (var i = 0; i < missing; i++)
                _hitPublisher.Publish(new BalloonHitMessage(fakeModel, Vector3.zero));
        }
    }
}
#endif