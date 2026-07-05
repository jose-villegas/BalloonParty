using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Routes each balloon's hit reaction to its owning controller. Balloons do not subscribe
    ///     to the global <see cref="ActorHitMessage"/> stream — that cost O(active balloons)
    ///     delegate invocations per hit plus subscription allocations per spawn — instead
    ///     <c>HitPipeline</c> resolves the owner here via the model's
    ///     <see cref="IBalloonModel.RegistryHandle"/>: a flat-array index, no hashing. The handle
    ///     travels with the model, so routing is move-invariant (balance moves and pressure
    ///     shoves never touch the registry), and a reference compare on resolve stands in for a
    ///     generation counter — models are never pooled, so a stale handle can only point at
    ///     null or a different model. Also owns board-clear teardown: one subscriber iterating a
    ///     snapshot, rather than per-balloon subscriptions whose re-entrant disposal would need
    ///     hand-rolled guards.
    /// </summary>
    internal class BalloonControllerRegistry : IStartable, IDisposable
    {
        // Comfortably above the 66-slot board plus spawn/transit overlap, so growth is a
        // never-in-practice fallback rather than a steady-state event.
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

        // Internal for direct test invocation of the resolve mechanics.
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
            // Snapshot first — HandleBoardClear returns views to the pool, and controllers
            // popped-but-waiting-for-item-activation are still registered and must be included.
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
                controller.HandleBoardClear(msg.PlayPopVfx);
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
