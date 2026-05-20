using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    public readonly struct SlotGridChangedEvent
    {
        public readonly Vector2Int Index;
        public readonly SlotGridChangeType ChangeType;

        public SlotGridChangedEvent(Vector2Int index, SlotGridChangeType changeType)
        {
            Index = index;
            ChangeType = changeType;
        }
    }

    public enum SlotGridChangeType
    {
        Placed,
        Removed
    }
}
