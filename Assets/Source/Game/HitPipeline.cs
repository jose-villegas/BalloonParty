using BalloonParty.Balloon.Controller;
using BalloonParty.Game.Score;
using BalloonParty.Shared.Messages;
using MessagePipe;
using VContainer;

namespace BalloonParty.Game
{
    /// <summary>
    ///     Owns the order-dependent part of hit resolution. MessagePipe dispatches in
    ///     subscription order — which equals registration order and is enforced nowhere — so the
    ///     stages with a required sequence are invoked here explicitly. The trailing broadcast
    ///     serves only order-independent observers: nudges, VFX, grid-actor removal, diagnostics,
    ///     and item activation (which defers itself a frame regardless of bus position).
    /// </summary>
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
            // Streak/score first: ProjectileHitResolver reads the streak tracker immediately
            // after dispatching to apply the streak-shield rule.
            _score.OnActorHit(msg);

            // Then the owning balloon's reaction (pop/deflect/pass-through), routed directly to
            // its controller instead of broadcast-and-filter across every live balloon.
            _balloonRegistry.Route(msg);

            _hitPublisher.Publish(msg);
        }
    }
}
