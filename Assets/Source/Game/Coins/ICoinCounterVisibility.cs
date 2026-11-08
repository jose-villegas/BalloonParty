using UniRx;

namespace BalloonParty.Game.Coins
{
    /// <summary>Whether the coin counter shows: once it does, it stays for the session.</summary>
    internal interface ICoinCounterVisibility
    {
        IReadOnlyReactiveProperty<bool> Visible { get; }
    }
}
