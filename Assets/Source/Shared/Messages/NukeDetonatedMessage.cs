using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published by <c>NukeBlast</c> when an armed nuke's fuse runs out and it goes off. <see cref="Position" /> is the nuke's.
    /// </summary>
    public readonly struct NukeDetonatedMessage
    {
        public Vector3 Position { get; }

        public NukeDetonatedMessage(Vector3 position)
        {
            Position = position;
        }
    }
}
