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

namespace BalloonParty.Item
{
    internal class ItemActivator : IStartable, IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly IEnumerable<IBalloonItem> _handlers;
        private readonly IPublisher<ItemActivatedMessage> _itemActivatedPublisher;
        private readonly ISubscriber<ActorHitMessage> _hitSubscriber;

        private Dictionary<ItemType, IBalloonItem> _handlerMap;

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

            _hitSubscriber.Subscribe(OnActorHit);
        }

        public void Dispose()
        {
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

            ActivateAsync(handler, balloon, msg.WorldPosition).Forget();
        }

        private async UniTaskVoid ActivateAsync(IBalloonItem handler, IBalloonModel balloon, Vector3 worldPosition)
        {
            try
            {
                // Yield one frame so all synchronous ActorHitMessage subscribers
                // (e.g. BalloonController capturing item rotation) finish first.
                await UniTask.Yield(_cts.Token);

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
