using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>Published when a shot enters CRUISE — enough consecutive wall bounces with no balloon
    /// contact that it's clearly ping-ponging an empty corridor. Feedback systems hook here to make
    /// the long flight read as a reward (the motion resolver already accelerates it by its shields).</summary>
    internal readonly struct ProjectileCruiseStartedMessage
    {
        public readonly Vector3 WorldPosition;
        public readonly Vector3 Direction;
        public readonly int ShieldsRemaining;

        public ProjectileCruiseStartedMessage(Vector3 worldPosition, Vector3 direction, int shieldsRemaining)
        {
            WorldPosition = worldPosition;
            Direction = direction;
            ShieldsRemaining = shieldsRemaining;
        }
    }
}
