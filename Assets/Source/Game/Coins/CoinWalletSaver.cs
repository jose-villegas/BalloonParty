using System;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.Coins
{
    /// <summary>
    ///     Saves the wallet at its save points: on game over, when the run resets (catching coins that landed
    ///     after the game over), when the app may be about to stop, and when the game scope is torn down. Not
    ///     on every coin, because <c>PlayerPrefs.Save</c> is slow on mobile.
    /// </summary>
    internal sealed class CoinWalletSaver : IStartable, IDisposable
    {
        private readonly ICoinWalletPersistence _wallet;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ISubscriber<RunResetMessage> _runResetSubscriber;
        private readonly IAppSuspendSignal _suspendSignal;
        private readonly CompositeDisposable _subscriptions = new();

        public CoinWalletSaver(
            ICoinWalletPersistence wallet,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ISubscriber<RunResetMessage> runResetSubscriber,
            IAppSuspendSignal suspendSignal)
        {
            _wallet = wallet;
            _gameOverSubscriber = gameOverSubscriber;
            _runResetSubscriber = runResetSubscriber;
            _suspendSignal = suspendSignal;
        }

        public void Start()
        {
            _gameOverSubscriber.Subscribe(_ => _wallet.Save()).AddTo(_subscriptions);
            _runResetSubscriber.Subscribe(_ => _wallet.Save()).AddTo(_subscriptions);
            _suspendSignal.Suspending.Subscribe(_ => _wallet.Save()).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _wallet.Save();
        }
    }
}
