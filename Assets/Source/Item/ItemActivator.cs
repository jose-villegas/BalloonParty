using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Diagnostics;
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

        /// <summary>The handler for the item this hit releases, or null when it releases none. Only a pop releases
        /// an item: a coated carrier survives an ordinary hit (it loses the coat), and the item must stay with it
        /// until the hit that pops it.</summary>
        internal IBalloonItem HandlerReleasedBy(ActorHitMessage msg)
        {
            if ((msg.Outcome & HitOutcome.Pop) == 0 || msg.Actor is not IBalloonModel balloon)
            {
                return null;
            }

            if (balloon is not IHasItemSlot itemSlot || itemSlot.Item.Value == ItemType.None)
            {
                return null;
            }

            if (!_handlerMap.TryGetValue(itemSlot.Item.Value, out var handler))
            {
                Log.Error("ItemActivator",
                    $"OnActorHit: no handler registered for item type " +
                    $"\"{itemSlot.Item.Value}\" — this is a configuration bug.");
                return null;
            }

            return handler;
        }

        private void OnActorHit(ActorHitMessage msg)
        {
            var handler = HandlerReleasedBy(msg);
            if (handler == null)
            {
                return;
            }

            var balloon = (IBalloonModel)msg.Actor;
            var context = new ItemActivationContext(balloon, msg.WorldPosition, msg.ProjectileDirection, msg.Context);
            Log.Info("Item", $"{handler.Type} activated at ({msg.WorldPosition.x:F1}, {msg.WorldPosition.y:F1})");
            ActivateAsync(handler, context).Forget();
        }

        private async UniTaskVoid ActivateAsync(IBalloonItem handler, ItemActivationContext context)
        {
            try
            {
                // Yield one frame so synchronous ActorHitMessage subscribers finish first.
                await UniTask.Yield(_cts.Token);

                await handler.Activate(context);
                _itemActivatedPublisher.Publish(
                    new ItemActivatedMessage(context.Balloon, context.DamageContext.Projectile));
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
