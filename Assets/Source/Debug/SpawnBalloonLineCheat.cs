#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Debug
{
    public class SpawnBalloonLineCheat : ICheat
    {
        public string Name => "Spawn Balloon Line";
        public string Section => "Spawning";
        public IReadOnlyList<string> Tags => new[] { "balloons", "spawning" };

        private readonly IPublisher<SpawnBalloonLineMessage> _publisher;

        public SpawnBalloonLineCheat(IPublisher<SpawnBalloonLineMessage> publisher)
        {
            _publisher = publisher;
        }

        public void Execute()
        {
            _publisher.Publish(default);
        }
    }
}
#endif