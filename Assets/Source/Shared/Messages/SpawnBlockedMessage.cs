using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>Raised at the moment of the reject pop so the HP drain syncs with the visual.</summary>
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
