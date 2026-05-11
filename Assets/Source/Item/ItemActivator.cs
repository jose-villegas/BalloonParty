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
    public class ItemActivator : IStartable
    {
        private readonly IEnumerable<IBalloonItem> _handlers;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly IPublisher<ItemActivatedMessage> _itemActivatedPublisher;

        [Inject]
        public ItemActivator(
            IEnumerable<IBalloonItem> handlers,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            IPublisher<ItemActivatedMessage> itemActivatedPublisher)
        {
            _handlers = handlers;
            _hitSubscriber = hitSubscriber;
            _itemActivatedPublisher = itemActivatedPublisher;
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
            // Yield one frame so all synchronous BalloonHitMessage subscribers
            // (e.g. BalloonController capturing item rotation) finish first.
            await UniTask.Yield();

            handler.Setup(msg.Balloon, msg.WorldPosition);
            await handler.Activate();
            _itemActivatedPublisher.Publish(new ItemActivatedMessage(msg.Balloon));
        }
    }
}
