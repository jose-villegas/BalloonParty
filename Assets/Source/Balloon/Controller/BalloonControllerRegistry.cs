using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>Routes each balloon's hit reaction to its owning controller via a flat-array index, avoiding per-balloon subscriptions.</summary>
    internal class BalloonControllerRegistry : IStartable, IDisposable
    {
        // Comfortably above the 66-slot board plus spawn/transit overlap.
        private const int InitialCapacity = 128;
        private const int Unregistered = -1;

        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly List<BalloonController> _clearBuffer = new();
        private readonly List<BalloonController> _outgoing = new();

        private IBalloonModel[] _models = new IBalloonModel[InitialCapacity];
        private BalloonController[] _controllers = new BalloonController[InitialCapacity];
        private int[] _freeIndices = new int[InitialCapacity];
        private int _freeCount;
        private int _highWater;
        private IDisposable _subscription;

        [Inject]
        internal BalloonControllerRegistry(ISubscriber<BoardClearMessage> boardClearSubscriber)
        {
            _boardClearSubscriber = boardClearSubscriber;
        }

        public void Start()
        {
            _subscription = _boardClearSubscriber.Subscribe(OnBoardClear);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        internal void Register(IWriteableBalloonModel model, BalloonController controller)
        {
            int index;
            if (_freeCount > 0)
            {
                index = _freeIndices[--_freeCount];
            }
            else
            {
                if (_highWater == _models.Length)
                {
                    Grow();
                }

                index = _highWater++;
            }

            _models[index] = model;
            _controllers[index] = controller;
            model.RegistryHandle = index;
        }

        internal void Unregister(IWriteableBalloonModel model)
        {
            var index = model.RegistryHandle;
            if (index < 0 || index >= _highWater || !ReferenceEquals(_models[index], model))
            {
                return;
            }

            _models[index] = null;
            _controllers[index] = null;
            model.RegistryHandle = Unregistered;
            _freeIndices[_freeCount++] = index;
        }

        internal void Route(ActorHitMessage msg)
        {
            if (msg.Actor is IBalloonModel balloon && TryResolve(balloon, out var controller))
            {
                controller.HandleHit(msg);
            }
        }

        // Pops one balloon on demand; routes through HandleBoardClear, not the projectile-hit Pop() path.
        // playPopVfx is false for effects that already animated the balloon away (e.g. the float dissolve).
        internal bool TryPopSingle(IBalloonModel model, bool playPopVfx = true)
        {
            if (!TryResolve(model, out var controller))
            {
                return false;
            }

            Unregister((IWriteableBalloonModel)model);
            controller.HandleBoardClear(playPopVfx);
            return true;
        }

        // Internal for direct test invocation.
        internal bool TryResolve(IBalloonModel model, out BalloonController controller)
        {
            var index = model.RegistryHandle;
            if (index >= 0 && index < _highWater && ReferenceEquals(_models[index], model))
            {
                controller = _controllers[index];
                return true;
            }

            controller = null;
            return false;
        }

        private void OnBoardClear(BoardClearMessage msg)
        {
            ClearAll(msg.PlayPopVfx);
        }

        // Callable directly so the level-transition Ascent can sweep item-pending stragglers.
        internal void ClearAll(bool playPopVfx)
        {
            // Snapshot first — HandleBoardClear returns views to the pool as it iterates.
            _clearBuffer.Clear();
            for (var i = 0; i < _highWater; i++)
            {
                if (_controllers[i] != null)
                {
                    _clearBuffer.Add(_controllers[i]);
                }

                _models[i] = null;
                _controllers[i] = null;
            }

            _freeCount = 0;
            _highWater = 0;

            foreach (var controller in _clearBuffer)
            {
                controller.HandleBoardClear(playPopVfx);
            }

            _clearBuffer.Clear();
        }

        // Graduates every live balloon into a detached "outgoing" group for a level transition: unregisters
        // them (so the new level's spawn and its ClearAll ignore them) and reparents their views under the
        // outgoing root, collecting the views for the transition to animate. Hand them back with ReturnOutgoing.
        internal void DetachOutgoing(Transform outgoingRoot, float exitDrop, List<ISlotActorView> views)
        {
            _outgoing.Clear();
            for (var i = 0; i < _highWater; i++)
            {
                var controller = _controllers[i];
                if (controller != null)
                {
                    views.Add(controller.DetachForOutgoing(outgoingRoot, exitDrop));
                    _outgoing.Add(controller);
                }

                _models[i] = null;
                _controllers[i] = null;
            }

            _freeCount = 0;
            _highWater = 0;
        }

        internal void ReturnOutgoing()
        {
            for (var i = 0; i < _outgoing.Count; i++)
            {
                _outgoing[i].ReturnToPool();
            }

            _outgoing.Clear();
        }

        private void Grow()
        {
            var capacity = _models.Length * 2;
            Array.Resize(ref _models, capacity);
            Array.Resize(ref _controllers, capacity);
            Array.Resize(ref _freeIndices, capacity);
        }
    }
}
