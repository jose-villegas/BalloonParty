using BalloonParty.Configuration.Balloons;
using VContainer;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Draws the disco ball's wave on the <c>BalloonParty/Sprite/DiscoWave</c> material, whose shader draws the
    ///     rainbow edge and the wash from the radius and progress (<see cref="GrowingDiscView" />).
    /// </summary>
    internal sealed class DiscoWaveView : GrowingDiscView, IDiscoWaveView
    {
        [Inject]
        internal void Construct(IBalloonsConfiguration balloons)
        {
            var settings = balloons.DiscoWave;
            Build(settings.DiscMaterial, settings.SortingLayerName, settings.SortingOrder, "DiscoWaveView");
        }
    }
}
