using UniRx;

namespace BalloonParty.Game.Coins
{
    /// <summary>The player's coin balance, read-only: what views and tools inject.</summary>
    internal interface ICoinBalance
    {
        IReadOnlyReactiveProperty<int> Balance { get; }
    }
}
