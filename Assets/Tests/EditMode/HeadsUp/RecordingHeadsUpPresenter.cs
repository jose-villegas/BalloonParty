using System;
using System.Collections.Generic;
using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.HeadsUp;
using UniRx;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>
    ///     Records what the service tells the view, in order. Hand-written: NSubstitute's argument matchers can't
    ///     bind the <c>in</c> request parameter.
    /// </summary>
    internal sealed class RecordingHeadsUpPresenter : IHeadsUpPresenter, IDisposable
    {
        internal readonly List<Call> Calls = new();
        private readonly Subject<HeadsUpHandle> _finished = new();

        /// <summary>Reports every shown line finished at once, like a line the view can't place.</summary>
        internal bool FinishOnShow;

        public IObservable<HeadsUpHandle> Finished => _finished;

        public void Show(HeadsUpHandle handle, in HeadsUpRequest request, HeadsUpStyleSettings style)
        {
            Calls.Add(new Call(CallKind.Show, handle, request, style));
            if (FinishOnShow)
            {
                _finished.OnNext(handle);
            }
        }

        public void Rewrite(HeadsUpHandle handle, in HeadsUpRequest request, HeadsUpStyleSettings style)
        {
            Calls.Add(new Call(CallKind.Rewrite, handle, request, style));
        }

        public void Release(HeadsUpHandle handle)
        {
            Calls.Add(new Call(CallKind.Release, handle));
        }

        public void ClearAll(bool animated)
        {
            Calls.Add(new Call(animated ? CallKind.ClearAnimated : CallKind.ClearAtOnce, HeadsUpHandle.None));
        }

        public void Advance(float liveDelta, float unscaledDelta)
        {
            Calls.Add(new Call(CallKind.Advance, HeadsUpHandle.None, liveDelta: liveDelta,
                unscaledDelta: unscaledDelta));
        }

        public void Dispose()
        {
            _finished.Dispose();
        }

        internal void Finish(HeadsUpHandle handle)
        {
            _finished.OnNext(handle);
        }

        internal List<Call> Of(CallKind kind)
        {
            return Calls.FindAll(call => call.Kind == kind);
        }

        internal enum CallKind
        {
            Show,
            Rewrite,
            Release,
            ClearAtOnce,
            ClearAnimated,
            Advance
        }

        internal readonly struct Call
        {
            internal readonly CallKind Kind;
            internal readonly HeadsUpHandle Handle;
            internal readonly HeadsUpRequest Request;
            internal readonly HeadsUpStyleSettings Style;
            internal readonly float LiveDelta;
            internal readonly float UnscaledDelta;

            internal Call(CallKind kind, HeadsUpHandle handle, HeadsUpRequest request = default,
                HeadsUpStyleSettings style = null, float liveDelta = 0f, float unscaledDelta = 0f)
            {
                Kind = kind;
                Handle = handle;
                Request = request;
                Style = style;
                LiveDelta = liveDelta;
                UnscaledDelta = unscaledDelta;
            }
        }
    }
}
