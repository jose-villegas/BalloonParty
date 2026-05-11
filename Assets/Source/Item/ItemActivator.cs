#region

using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using VContainer.Unity;

#endregion

namespace BalloonParty.Item
{
    /// <summary>
    ///     Central orchestrator for item balloon activations. Listens for <see cref="BalloonHitMessage" />
    ///     filtered to balloons that carry an item. Delegates to the matching <see cref="IBalloonItem" />
    ///     handler, awaits the effect, then publishes <see cref="ItemActivatedMessage" /> so the balloon
    ///     controller can return the balloon to pool.
    /// </summary>
    public class ItemActivator : IStartable
    {
        private readonly IEnumerable<IBalloonItem> _handlers;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly IPublisher<ItemActivatedMessage> _itemActivatedPublisher;
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;

        [Inject]
        public ItemActivator(
            IEnumerable<IBalloonItem> handlers,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            IPublisher<ItemActivatedMessage> itemActivatedPublisher,
            IPublisher<BalanceBalloonsMessage> balancePublisher)
        {
            _handlers = handlers;
            _hitSubscriber = hitSubscriber;
            _itemActivatedPublisher = itemActivatedPublisher;
            _balancePublisher = balancePublisher;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnBalloonHit);
        }

        private void OnBalloonHit(BalloonHitMessage msg)
        {
            if (msg.Balloon.Item.Value == ItemType.None)
            {
                return;
            }

            var handler = _handlers.FirstOrDefault(h => h.Type == msg.Balloon.Item.Value);
            if (handler == null)
            {
                return;
            }

            ActivateAsync(handler, msg).Forget();
        }

        private async UniTaskVoid ActivateAsync(IBalloonItem handler, BalloonHitMessage msg)
        {
            handler.Setup(msg.Balloon, msg.WorldPosition);
            await handler.Activate();
            _itemActivatedPublisher.Publish(new ItemActivatedMessage(msg.Balloon));
            _balancePublisher.Publish(new BalanceBalloonsMessage());
        }
    }
}

