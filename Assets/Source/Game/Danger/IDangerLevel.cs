using UniRx;

namespace BalloonParty.Game.Danger
{
    /// <summary>
    ///     Read-only 0→1 "how close are we to dying" signal, consumed by the danger gradient HUD.
    /// </summary>
    internal interface IDangerLevel
    {
        IReadOnlyReactiveProperty<float> Level { get; }
    }
}
