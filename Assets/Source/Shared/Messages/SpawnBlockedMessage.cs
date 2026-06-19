using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Published once per balloon that could not be spawned because its column is
    ///     saturated — the would-be balloon pops at the entry line instead. Each message
    ///     costs the player one hit point and drives the reject feedback (camera shake).
    ///     Raised at the moment of the pop so the HP drain syncs with the visual.
    /// </summary>
    public readonly struct SpawnBlockedMessage
    {
        public readonly int Column;
        public readonly Vector3 Position;

        public SpawnBlockedMessage(int column, Vector3 position)
        {
            Column = column;
            Position = position;
        }
    }
}
