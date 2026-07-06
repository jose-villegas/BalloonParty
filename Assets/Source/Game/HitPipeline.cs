using BalloonParty.Balloon.Controller;
using BalloonParty.Game.Score;
using BalloonParty.Shared.Messages;
using MessagePipe;
using VContainer;

namespace BalloonParty.Game
{
    /// <summary>Owns the order-dependent part of hit resolution explicitly, since MessagePipe's subscription order is enforced nowhere.</summary>
    internal class HitPipeline : IHitDispatcher
    {
        private readonly ScoreController _score;
        private readonly BalloonControllerRegistry _balloonRegistry;
        private readonly IPublisher<ActorHitMessage> _hitPublisher;

        [Inject]
        internal HitPipeline(
            ScoreController score,
            BalloonControllerRegistry balloonRegistry,
            IPublisher<ActorHitMessage> hitPublisher)
        {
            _score = score;
            _balloonRegistry = balloonRegistry;
            _hitPublisher = hitPublisher;
        }

        public void Dispatch(ActorHitMessage msg)
        {
            // Streak/score first: ProjectileHitResolver reads the streak tracker right after dispatch.
            _score.OnActorHit(msg);

            _balloonRegistry.Route(msg);

            _hitPublisher.Publish(msg);
        }
    }
}
