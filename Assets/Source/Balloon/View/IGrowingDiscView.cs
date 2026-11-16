using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>A disc grown from a pop to the radius its effect has reached.</summary>
    internal interface IGrowingDiscView
    {
        void Show(Vector3 center);

        /// <param name="radius">World radius of the disc.</param>
        /// <param name="progress">0 at the pop, 1 when the disc covers the board.</param>
        void SetRadius(float radius, float progress);

        void Hide();
    }
}
