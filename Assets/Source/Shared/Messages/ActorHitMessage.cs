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
        public readonly HitOutcome Outcome;

        public ActorHitMessage(
            ISlotActor actor,
            Vector3 worldPosition,
            Vector3 projectileDirection,
            HitOutcome outcome,
            int damage = 1)
        {
            Actor = actor;
            WorldPosition = worldPosition;
            ProjectileDirection = projectileDirection;
            Outcome = outcome;
            Damage = damage;
        }
    }
}
