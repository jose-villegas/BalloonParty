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
    ///     <c>HitPipeline</c> resolves the owner here. Also owns board-clear teardown: one
    ///     subscriber iterating a snapshot replaces the per-balloon subscriptions whose
    ///     re-entrant disposal needed hand-rolled guards.
    /// </summary>
    internal class BalloonControllerRegistry : IStartable, IDisposable
    {
        private readonly Dictionary<IBalloonModel, BalloonController> _controllers = new();
        private readonly List<BalloonController> _clearBuffer = new();
        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
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

        internal void Register(IBalloonModel model, BalloonController controller)
        {
            _controllers[model] = controller;
        }

        internal void Unregister(IBalloonModel model)
        {
            _controllers.Remove(model);
        }

        internal void Route(ActorHitMessage msg)
        {
            if (msg.Actor is IBalloonModel balloon && _controllers.TryGetValue(balloon, out var controller))
            {
                controller.HandleHit(msg);
            }
        }

        private void OnBoardClear(BoardClearMessage _)
        {
            // Snapshot first — HandleBoardClear returns views to the pool, and controllers
            // popped-but-waiting-for-item-activation are still registered and must be included.
            _clearBuffer.Clear();
            _clearBuffer.AddRange(_controllers.Values);
            _controllers.Clear();

            foreach (var controller in _clearBuffer)
            {
                controller.HandleBoardClear();
            }

            _clearBuffer.Clear();
        }
    }
}
