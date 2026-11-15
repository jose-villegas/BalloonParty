using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>The disco ball's wave as drawn: a disc's edge at the radius the wave has reached.</summary>
    internal interface IDiscoWaveView
    {
        void Show(Vector3 center);

        void SetRadius(float radius);

        void Hide();
    }
}
