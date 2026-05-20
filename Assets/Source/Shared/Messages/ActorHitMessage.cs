using BalloonParty.Slots;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ActorHitMessage
    {
        public readonly ISlotActor Actor;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 ProjectileDirection;

        public readonly int Damage;

        public ActorHitMessage(
            ISlotActor actor,
            Vector3 worldPosition,
            Vector3 projectileDirection,
            int damage = 1)
        {
            Actor = actor;
            WorldPosition = worldPosition;
            ProjectileDirection = projectileDirection;
            Damage = damage;
        }
    }
}

