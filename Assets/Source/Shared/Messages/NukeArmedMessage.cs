using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published by <c>NukeFuse</c> when a nuke is armed (PLAN-NukeBalloon) and its fuse starts. <see cref="Position" /> is the nuke's.
    /// </summary>
    public readonly struct NukeArmedMessage
    {
        public Vector3 Position { get; }

        public NukeArmedMessage(Vector3 position)
        {
            Position = position;
        }
    }
}
