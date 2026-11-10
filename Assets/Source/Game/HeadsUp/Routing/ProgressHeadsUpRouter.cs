using System;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.HeadsUp.Routing
{
    /// <summary>
    ///     The run's heads-ups: <c>-1</c> at the heart when a blocked line's strikethrough costs one, and
    ///     <c>{colour} done</c> at a colour's bar when it fills, from which that colour scores nothing.
    /// </summary>
    internal sealed class ProgressHeadsUpRouter : IStartable, IDisposable
    {
        private readonly IHeadsUps _headsUps;
        private readonly ISubscriber<StrikethroughArrivedMessage> _strikethroughSubscriber;
        private readonly ISubscriber<ProgressBarCompletedMessage> _barSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        internal ProgressHeadsUpRouter(
            IHeadsUps headsUps,
            ISubscriber<StrikethroughArrivedMessage> strikethroughSubscriber,
            ISubscriber<ProgressBarCompletedMessage> barSubscriber)
        {
            _headsUps = headsUps;
            _strikethroughSubscriber = strikethroughSubscriber;
            _barSubscriber = barSubscriber;
        }

        public void Start()
        {
            _strikethroughSubscriber.Subscribe(OnStrikethrough).AddTo(_subscriptions);
            _barSubscriber.Subscribe(OnBarCompleted).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnStrikethrough(StrikethroughArrivedMessage msg)
        {
            _headsUps.Request(new HeadsUpRequest(
                HeadsUpStyleId.HeartLost, 1, HeadsUpAnchor.Endpoint(TrailEndpointKeys.Heart)));
        }

        // Each bar is its colour's trail endpoint, keyed by the colour's name.
        private void OnBarCompleted(ProgressBarCompletedMessage msg)
        {
            _headsUps.Request(new HeadsUpRequest(
                HeadsUpStyleId.ColorDone, 0, HeadsUpAnchor.Endpoint(msg.ColorName), word: msg.ColorName));
        }
    }
}
