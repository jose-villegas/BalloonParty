using System;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using VContainer.Unity;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     Shows the Sharpshooter card at work: the first time the run wins it (a level-up's pick or a golden balloon's
    ///     free card), the next ball loaded is a Snipe lance whatever the card's chance (<see cref="ISnipeGuarantee" />).
    ///     A card an ad lends for one retry isn't won, so it doesn't count. Once a run: a retry keeps it given.
    /// </summary>
    internal sealed class SharpshooterSnipeGift : IStartable, IRunResettable, IDisposable
    {
        private readonly ISubscriber<UpgradeCardHeldMessage> _held;
        private readonly ISnipeGuarantee _guarantee;
        private readonly IRetryState _retryState;

        private IDisposable _subscription;
        private bool _given;

        // Before RetryTracker (Score + 10) consumes the staged retry level this reads.
        public int ResetOrder => RunResetOrder.Counters;

        internal SharpshooterSnipeGift(
            ISubscriber<UpgradeCardHeldMessage> held, ISnipeGuarantee guarantee, IRetryState retryState)
        {
            _held = held;
            _guarantee = guarantee;
            _retryState = retryState;
        }

        public void Start()
        {
            _subscription = _held.Subscribe(OnHeld);
        }

        public void ResetRun(int generation)
        {
            if (_retryState.RetryLevel <= 0)
            {
                _given = false;
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void OnHeld(UpgradeCardHeldMessage message)
        {
            if (_given || message.Card is not { Kind: UpgradeKind.SnipeChance })
            {
                return;
            }

            _given = true;
            _guarantee.GuaranteeNext();
        }
    }
}
