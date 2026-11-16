using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published by <c>NukeBlast</c> when its blast has cleared the board and the spawn clock's
    ///     breather starts. <see cref="Position" /> is the nuke's.
    /// </summary>
    public readonly struct NukeBreatherMessage
    {
        public Vector3 Position { get; }

        public NukeBreatherMessage(Vector3 position)
        {
            Position = position;
        }
    }
}
