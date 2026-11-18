using UniRx;

namespace BalloonParty.Slots.Capabilities
{
    /// <summary>
    ///     An actor that can wear foam: layers a ripe bubble cluster hands it when its fuse runs out. While it has any, an
    ///     ordinary hit takes one layer off and leaves the actor itself untouched.
    /// </summary>
    public interface ICoatable
    {
        IReadOnlyReactiveProperty<int> Coat { get; }
    }
}
