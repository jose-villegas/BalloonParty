using System;
using System.Collections.Generic;
using BalloonParty.Game.HeadsUp;
using MessagePipe;
using NSubstitute;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>
    ///     Records what the routers ask for. Hand-written: NSubstitute's argument matchers can't bind the
    ///     <c>in</c> request parameter.
    /// </summary>
    internal sealed class RecordingHeadsUps : IHeadsUps
    {
        internal readonly List<HeadsUpRequest> Requests = new();
        internal readonly List<HeadsUpHandle> Released = new();

        public HeadsUpHandle Request(in HeadsUpRequest request)
        {
            Requests.Add(request);
            return new HeadsUpHandle(Requests.Count - 1, 1);
        }

        public void Release(HeadsUpHandle handle)
        {
            Released.Add(handle);
        }
    }

    internal static class HeadsUpRouterTestKit
    {
        internal static ISubscriber<T> CaptureSubscriber<T>(Action<IMessageHandler<T>> capture)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber.Subscribe(Arg.Do(capture), Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}
