using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>Asks the speck field to enable a burst of specks at a world position, using the profile that
    /// matches <see cref="Source" /> — the request analogue of DisturbanceFieldService.Stamp.</summary>
    internal readonly struct SpeckSpawnRequestMessage
    {
        public readonly SpeckSource Source;
        public readonly Vector3 WorldPosition;

        public SpeckSpawnRequestMessage(SpeckSource source, Vector3 worldPosition)
        {
            Source = source;
            WorldPosition = worldPosition;
        }
    }
}
