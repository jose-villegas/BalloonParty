namespace BalloonParty.Game.Coins
{
    /// <summary>Where the wallet's balance is kept between sessions.</summary>
    internal interface ICoinStore
    {
        int Load();

        void Save(int balance);
    }
}
