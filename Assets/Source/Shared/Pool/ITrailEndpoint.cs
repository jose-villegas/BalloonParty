using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     A named anchor a trail flies to or from — a fixed point in the world or UI that a trail
    ///     controller resolves by key through <see cref="TrailEndpointRegistry" />, instead of each
    ///     feature wiring its own position provider. <see cref="RandomPosition" /> lets an endpoint
    ///     spread arrivals across an area (a progress bar); a point anchor just returns <see cref="Center" />.
    /// </summary>
    internal interface ITrailEndpoint
    {
        Vector3 Center { get; }

        Vector3 RandomPosition();
    }
}
