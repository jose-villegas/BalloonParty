using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item
{
    internal class ItemActivator : IStartable, IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly IEnumerable<IBalloonItem> _handlers;
        private readonly IPublisher<ItemActivatedMessage> _itemActivatedPublisher;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;

        private Dictionary<ItemType, IBalloonItem> _handlerMap;
        private IDisposable _subscription;

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
            _handlerMap = new Dictionary<ItemType, IBalloonItem>();
            foreach (var h in _handlers)
            {
                _handlerMap[h.Type] = h;
            }

            _subscription = _hitSubscriber.Subscribe(OnActorHit);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _cts.Cancel();
            _cts.Dispose();
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

            if (!_handlerMap.TryGetValue(itemSlot.Item.Value, out var handler))
            {
                Debug.LogError(
                    $"ItemActivator.OnActorHit: no handler registered for item type " +
                    $"\"{itemSlot.Item.Value}\" — this is a configuration bug.");
                return;
            }

            var context = new ItemActivationContext(balloon, msg.WorldPosition, msg.ProjectileDirection, msg.Context);
            ActivateAsync(handler, context).Forget();
        }

        private async UniTaskVoid ActivateAsync(IBalloonItem handler, ItemActivationContext context)
        {
            try
            {
                // Yield one frame so synchronous ActorHitMessage subscribers finish first.
                await UniTask.Yield(_cts.Token);

                await handler.Activate(context);
                _itemActivatedPublisher.Publish(new ItemActivatedMessage(context.Balloon));
            }
            catch (OperationCanceledException)
            {
                // Expected during teardown.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
