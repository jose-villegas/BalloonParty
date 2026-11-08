namespace BalloonParty.Game.Coins
{
    /// <summary>Writes the wallet to storage. Only <see cref="CoinWalletSaver" /> uses it.</summary>
    internal interface ICoinWalletPersistence
    {
        /// <summary>Writes the balance to storage, if it changed since the last save.</summary>
        void Save();
    }
}
