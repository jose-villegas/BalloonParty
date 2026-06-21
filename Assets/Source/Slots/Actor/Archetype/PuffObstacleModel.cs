using BalloonParty.Slots.Actor.Cluster;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    internal class PuffObstacleModel : IClusterableSlotActor, IPassThrough, IGridActorModel
    {
        public Vector2Int SlotIndex { get; private set; }
        public int ClusterId { get; set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public GridActorType ActorType => GridActorType.Puff;
        public SlotActorKind Kind => SlotActorKind.Static;
    }
}
