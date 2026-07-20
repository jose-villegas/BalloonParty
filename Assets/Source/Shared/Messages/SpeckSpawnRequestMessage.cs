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

        /// <summary>The shot's normalized velocity t in [0,1] at the spawn moment — only meaningful for
        /// <see cref="SpeckSource.ProjectileCruise" />'s velocity-scaled profile; ignored by every other source.</summary>
        public readonly float VelocityT;

        public SpeckSpawnRequestMessage(SpeckSource source, Vector3 worldPosition, float velocityT = 0f)
        {
            Source = source;
            WorldPosition = worldPosition;
            VelocityT = velocityT;
        }
    }
}
