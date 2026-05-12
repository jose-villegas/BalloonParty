using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ShieldGainedMessage
    {
        public readonly Vector2Int SlotIndex;

        public ShieldGainedMessage(Vector2Int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }
}

