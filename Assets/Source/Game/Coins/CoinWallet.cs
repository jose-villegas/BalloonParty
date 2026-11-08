using System;
using UniRx;

namespace BalloonParty.Game.Coins
{
    /// <inheritdoc cref="ICoinWallet" />
    internal sealed class CoinWallet : ICoinWallet, ICoinWalletPersistence
    {
        private readonly ICoinStore _store;
        private readonly ReactiveProperty<int> _balance;

        private bool _dirty;

        public IReadOnlyReactiveProperty<int> Balance => _balance;

        public CoinWallet(ICoinStore store)
        {
            _store = store;
            _balance = new ReactiveProperty<int>(Math.Max(0, store.Load()));
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _balance.Value += amount;
            _dirty = true;
        }

        // Saved at once: a spend is a rare button press, and one lost to a crash would refund the purchase.
        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > _balance.Value)
            {
                return false;
            }

            if (amount > 0)
            {
                _balance.Value -= amount;
                _dirty = true;
                Save();
            }

            return true;
        }

        public void Save()
        {
            if (!_dirty)
            {
                return;
            }

            _store.Save(_balance.Value);
            _dirty = false;
        }
    }
}
