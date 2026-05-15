using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Item
{
    internal class ItemActivator : IStartable
    {
        private readonly IEnumerable<IBalloonItem> _handlers;
        private readonly IPublisher<ItemActivatedMessage> _itemActivatedPublisher;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;

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
                Debug.LogError(
                    $"ItemActivator.OnBalloonHit: no handler registered for item type " +
                    $"\"{msg.Balloon.Item.Value}\" — this is a configuration bug.");
                return;
            }

            ActivateAsync(handler, msg).Forget();
        }

        private async UniTaskVoid ActivateAsync(IBalloonItem handler, BalloonHitMessage msg)
        {
            try
            {
                // Yield one frame so all synchronous BalloonHitMessage subscribers
                // (e.g. BalloonController capturing item rotation) finish first.
                await UniTask.Yield();

                handler.Setup(msg.Balloon, msg.WorldPosition);
                await handler.Activate();
                _itemActivatedPublisher.Publish(new ItemActivatedMessage(msg.Balloon));
            }
            catch (OperationCanceledException)
            {
                // Expected during teardown — swallow silently.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
