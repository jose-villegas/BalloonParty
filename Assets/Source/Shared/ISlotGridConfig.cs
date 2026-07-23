using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>Slot grid layout: size, spacing, and origin offset.</summary>
    public interface ISlotGridConfig
    {
        Vector2Int SlotsSize { get; }
        Vector2 SlotSeparation { get; }
        Vector2 SlotsOffset { get; }
    }
}
