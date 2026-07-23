using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Slot Grid Config", fileName = "SlotGridConfig")]
    internal class SlotGridConfig : ScriptableObject, ISlotGridConfig
    {
        [Header("Slots")]
        [SerializeField] private Vector2Int _slotsSize;
        [SerializeField] private Vector2 _slotSeparation;
        [SerializeField] private Vector2 _slotsOffset;

        public Vector2Int SlotsSize => _slotsSize;
        public Vector2 SlotSeparation => _slotSeparation;
        public Vector2 SlotsOffset => _slotsOffset;
    }
}
