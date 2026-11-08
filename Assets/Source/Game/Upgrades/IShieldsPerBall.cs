using UniRx;

namespace BalloonParty.Game.Upgrades
{
    /// <summary>
    ///     The shields every new ball starts with, as a value the HUD can watch: the configured count plus
    ///     shield cards. It changes only when the cards held change, never during a flight.
    /// </summary>
    internal interface IShieldsPerBall
    {
        IReadOnlyReactiveProperty<int> ShieldsPerBall { get; }
    }
}
