using System;
using UniRx;
using UnityEngine;

namespace BalloonParty.Game.Coins
{
    /// <inheritdoc cref="IAppSuspendSignal" />
    internal sealed class AppSuspendSignal : IAppSuspendSignal, IDisposable
    {
        private readonly Subject<Unit> _suspending = new();

        public IObservable<Unit> Suspending => _suspending;

        public AppSuspendSignal()
        {
            Application.focusChanged += OnFocusChanged;
            Application.quitting += OnQuitting;
        }

        public void Dispose()
        {
            Application.focusChanged -= OnFocusChanged;
            Application.quitting -= OnQuitting;
            _suspending.Dispose();
        }

        private void OnFocusChanged(bool focused)
        {
            if (!focused)
            {
                _suspending.OnNext(Unit.Default);
            }
        }

        private void OnQuitting()
        {
            _suspending.OnNext(Unit.Default);
        }
    }
}
