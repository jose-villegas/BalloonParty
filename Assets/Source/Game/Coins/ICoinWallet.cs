namespace BalloonParty.Game.Coins
{
    /// <summary>
    ///     The player's coins, kept across runs and app restarts. Earned coins are held in memory until
    ///     <see cref="CoinWalletSaver" /> saves at a save point; a spend is saved at once.
    /// </summary>
    internal interface ICoinWallet : ICoinBalance
    {
        /// <summary>Adds coins; a zero or negative amount is ignored.</summary>
        void Add(int amount);

        /// <summary>Takes the coins if the balance covers them; otherwise changes nothing and returns false.</summary>
        bool TrySpend(int amount);
    }
}
