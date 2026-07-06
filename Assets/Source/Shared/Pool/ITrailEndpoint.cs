using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>A named anchor a trail flies to or from, resolved by key through <see cref="TrailEndpointRegistry" />.</summary>
    internal interface ITrailEndpoint
    {
        Vector3 Center { get; }

        Vector3 RandomPosition();
    }
}
