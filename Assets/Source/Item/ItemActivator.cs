using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
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
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;

        [Inject]
        public ItemActivator(
            IEnumerable<IBalloonItem> handlers,
            ISubscriber<ActorHitMessage> hitSubscriber,
            IPublisher<ItemActivatedMessage> itemActivatedPublisher)
        {
            _handlers = handlers;
            _hitSubscriber = hitSubscriber;
            _itemActivatedPublisher = itemActivatedPublisher;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnActorHit);
        }

        private void OnActorHit(ActorHitMessage msg)
        {
            if (msg.Actor is not IBalloonModel balloon)
            {
                return;
            }

            if (balloon is not IHasItemSlot itemSlot || itemSlot.Item.Value == ItemType.None)
            {
                return;
            }

            var handler = _handlers.FirstOrDefault(h => h.Type == itemSlot.Item.Value);
            if (handler == null)
            {
                Debug.LogError(
                    $"ItemActivator.OnActorHit: no handler registered for item type " +
                    $"\"{itemSlot.Item.Value}\" — this is a configuration bug.");
                return;
            }

            ActivateAsync(handler, balloon, msg.WorldPosition).Forget();
        }

        private async UniTaskVoid ActivateAsync(IBalloonItem handler, IBalloonModel balloon, Vector3 worldPosition)
        {
            try
            {
                // Yield one frame so all synchronous ActorHitMessage subscribers
                // (e.g. BalloonController capturing item rotation) finish first.
                await UniTask.Yield();

                handler.Setup(balloon, worldPosition);
                await handler.Activate();
                _itemActivatedPublisher.Publish(new ItemActivatedMessage(balloon));
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
