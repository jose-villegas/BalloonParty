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
    internal class BalloonControllerRegistry : IStartable, IDisposable, ITransitionOutgoingContent
    {
        // Comfortably above the 66-slot board plus spawn/transit overlap.
        private const int InitialCapacity = 128;
        private const int Unregistered = -1;

        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly List<BalloonController> _clearBuffer = new();

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
        internal bool TryPopSingle(IBalloonModel model)
        {
            if (!TryResolve(model, out var controller))
            {
                return false;
            }

            Unregister((IWriteableBalloonModel)model);
            controller.HandleBoardClear(playPopVfx: true);
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

        // Outgoing balloons ride the descending scenario root out with the old level.
        public void HoldOutgoing(Transform outgoingRoot, float exitDrop)
        {
            for (var i = 0; i < _highWater; i++)
            {
                _controllers[i]?.RideOutgoing(outgoingRoot, exitDrop);
            }
        }

        public void ReleaseOutgoing()
        {
            // Nothing to undo — ClearAll already returns outgoing balloons to their pool.
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

        private void Grow()
        {
            var capacity = _models.Length * 2;
            Array.Resize(ref _models, capacity);
            Array.Resize(ref _controllers, capacity);
            Array.Resize(ref _freeIndices, capacity);
        }
    }
}
