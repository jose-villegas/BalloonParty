using System;
using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Disturbance;
using UniRx;

namespace BalloonParty.Shared.Extensions
{
    internal static class DisturbancePulseExtensions
    {
        /// <summary>Runs <paramref name="onPulse" /> every <c>Interval</c> seconds of the source's profile — the
        /// shared scaffold behind the constant emitters (tough warning, rainbow cycle, unbreakable burst).
        /// Returns the subscription for the caller to add to its disposables; a non-positive interval means "not
        /// a periodic emitter" and returns <see cref="Disposable.Empty" />.</summary>
        internal static IDisposable StartPulse(this DisturbanceFieldService field, StampSource source, Action onPulse)
        {
            var interval = field.GetProfile(source).Interval;
            return interval > 0f
                ? Observable.Interval(TimeSpan.FromSeconds(interval)).Subscribe(_ => onPulse())
                : Disposable.Empty;
        }
    }
}
