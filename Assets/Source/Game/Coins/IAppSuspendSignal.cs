using System;
using UniRx;

namespace BalloonParty.Game.Coins
{
    /// <summary>
    ///     Fires when the app may be about to stop: it lost focus (on mobile, sent to the background, where
    ///     the OS can kill it unseen) or it is quitting. The last moment to save.
    /// </summary>
    internal interface IAppSuspendSignal
    {
        IObservable<Unit> Suspending { get; }
    }
}
