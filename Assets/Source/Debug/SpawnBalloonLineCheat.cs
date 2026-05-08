#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using MessagePipe;
using BalloonParty.Shared.Messages;

namespace BalloonParty.Debug
{
    public class SpawnBalloonLineCheat : ICheat
    {
        private readonly IPublisher<SpawnBalloonLineMessage> _publisher;

        public string Name => "Spawn Balloon Line";
        public string Section => "Spawning";
        public IReadOnlyList<string> Tags => new[] { "balloons", "spawning" };

        public SpawnBalloonLineCheat(IPublisher<SpawnBalloonLineMessage> publisher)
        {
            _publisher = publisher;
        }

        public void Execute() => _publisher.Publish(default);
    }
}
#endif

