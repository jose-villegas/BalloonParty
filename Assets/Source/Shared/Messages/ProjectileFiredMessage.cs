using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>Published the instant a fired shot leaves the muzzle (its first free physics frame, so un-fired
    /// shots never emit it). Carries the muzzle world position and the fire heading so any system — the
    /// disturbance field, VFX, audio, camera shake — can react to the exact fire moment.</summary>
    internal readonly struct ProjectileFiredMessage
    {
        public readonly Vector3 WorldPosition;
        public readonly Vector3 Direction;

        public ProjectileFiredMessage(Vector3 worldPosition, Vector3 direction)
        {
            WorldPosition = worldPosition;
            Direction = direction;
        }
    }
}
