using System;
using BalloonParty.Shared.Messages;
using MessagePipe;
using VContainer.Unity;

namespace BalloonParty.Game.Coins
{
    /// <summary>
    ///     Adds coins to the wallet when they land in the counter (<see cref="CoinLandedMessage" />), not when
    ///     the pop drops them, so the balance never runs ahead of what the player has seen arrive.
    /// </summary>
    internal sealed class CoinCreditor : IStartable, IDisposable
    {
        private readonly ICoinWallet _wallet;
        private readonly ISubscriber<CoinLandedMessage> _landedSubscriber;

        private IDisposable _subscription;

        public CoinCreditor(ICoinWallet wallet, ISubscriber<CoinLandedMessage> landedSubscriber)
        {
            _wallet = wallet;
            _landedSubscriber = landedSubscriber;
        }

        public void Start()
        {
            _subscription = _landedSubscriber.Subscribe(message => _wallet.Add(message.Amount));
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
