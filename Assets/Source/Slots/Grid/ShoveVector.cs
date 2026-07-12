using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>A directed pressure impulse: normalized world-space direction originating at a slot.</summary>
    internal readonly struct ShoveVector
    {
        public static readonly ShoveVector None = default;

        public readonly Vector2 Direction;
        public readonly Vector2Int Origin;

        public bool Active => Direction != Vector2.zero;

        public ShoveVector(Vector2 direction, Vector2Int origin)
        {
            Direction = direction.normalized;
            Origin = origin;
        }
    }
}
