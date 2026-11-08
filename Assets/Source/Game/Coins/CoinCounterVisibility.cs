using System;
using BalloonParty.Game.Level;
using BalloonParty.Shared;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.Coins
{
    /// <summary>
    ///     Keeps the coin counter hidden until coins mean something to this player: the run reaches
    ///     <see cref="ICoinSettings.CoinsFromLevel" />, or the wallet already holds coins (a returning player,
    ///     who can spend them at any loss). After that it stays, so it never blinks out on a retry.
    /// </summary>
    internal sealed class CoinCounterVisibility : ICoinCounterVisibility, IInitializable, IDisposable
    {
        private readonly ICoinSettings _settings;
        private readonly ILevelProgress _levelProgress;
        private readonly ICoinBalance _balance;
        private readonly ReactiveProperty<bool> _visible = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public IReadOnlyReactiveProperty<bool> Visible => _visible;

        public CoinCounterVisibility(IRunConfig config, ILevelProgress levelProgress, ICoinBalance balance)
        {
            _settings = config.Coins;
            _levelProgress = levelProgress;
            _balance = balance;
        }

        // At initialization, before the views' Start, so a returning player's counter is there from the first
        // frame instead of fading in.
        public void Initialize()
        {
            _levelProgress.Level.Subscribe(level => ShowIf(level >= _settings.CoinsFromLevel)).AddTo(_subscriptions);
            _balance.Balance.Subscribe(balance => ShowIf(balance > 0)).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _visible.Dispose();
        }

        private void ShowIf(bool condition)
        {
            if (condition)
            {
                _visible.Value = true;
            }
        }
    }
}
