using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor
{
    internal class GridActorHitController : IStartable, IDisposable
    {
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;
        private readonly SlotGrid _grid;
        private IDisposable _subscription;

        [Inject]
        internal GridActorHitController(ISubscriber<ActorHitMessage> hitSubscriber, SlotGrid grid)
        {
            _hitSubscriber = hitSubscriber;
            _grid = grid;
        }

        public void Start()
        {
            _subscription = _hitSubscriber.Subscribe(OnActorHit);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        // Internal for direct test invocation — avoids MessagePipe infrastructure in tests.
        internal void OnActorHit(ActorHitMessage msg)
        {
            // BalloonController owns balloon removal; skip everything balloon-related.
            if (msg.Actor is IBalloonModel)
            {
                return;
            }

            if (msg.Actor is not IHasDurability durable)
            {
                return;
            }

            if (durable.HitsRemaining.Value > 0)
            {
                return;
            }

            _grid.Remove(msg.Actor.SlotIndex);
        }
    }
}

