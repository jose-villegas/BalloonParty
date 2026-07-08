using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Wraps a fixed anchor <see cref="Transform" /> as an <see cref="ITrailEndpoint" />.
    /// </summary>
    internal sealed class TransformTrailEndpoint : ITrailEndpoint
    {
        private readonly Transform _transform;

        public Vector3 Center => _transform.position;

        public TransformTrailEndpoint(Transform transform)
        {
            _transform = transform;
        }

        public Vector3 RandomPosition()
        {
            return Center;
        }
    }
}
