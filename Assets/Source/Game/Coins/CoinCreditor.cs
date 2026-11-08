using System;
using BalloonParty.Shared.Messages;
using MessagePipe;
using VContainer.Unity;

namespace BalloonParty.Game.Coins
{
    /// <summary>
    ///     Adds coins to the wallet. For now on <see cref="CoinDroppedMessage" />, the moment a pop drops one;
    ///     once coins fly to the counter, on their landing instead, so the drop itself never changes.
    /// </summary>
    internal sealed class CoinCreditor : IStartable, IDisposable
    {
        private readonly ICoinWallet _wallet;
        private readonly ISubscriber<CoinDroppedMessage> _droppedSubscriber;

        private IDisposable _subscription;

        public CoinCreditor(ICoinWallet wallet, ISubscriber<CoinDroppedMessage> droppedSubscriber)
        {
            _wallet = wallet;
            _droppedSubscriber = droppedSubscriber;
        }

        public void Start()
        {
            _subscription = _droppedSubscriber.Subscribe(message => _wallet.Add(message.Amount));
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
