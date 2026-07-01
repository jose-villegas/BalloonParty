using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Wraps a fixed anchor <see cref="Transform" /> as an <see cref="ITrailEndpoint" /> — the common
    ///     case of a UI icon a trail flies to or from. A point anchor has no area, so
    ///     <see cref="RandomPosition" /> is just <see cref="Center" />.
    /// </summary>
    internal sealed class TransformTrailEndpoint : ITrailEndpoint
    {
        private readonly Transform _transform;

        public TransformTrailEndpoint(Transform transform)
        {
            _transform = transform;
        }

        public Vector3 Center => _transform.position;

        public Vector3 RandomPosition()
        {
            return Center;
        }
    }
}
