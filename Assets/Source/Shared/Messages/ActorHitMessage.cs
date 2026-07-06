using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ActorHitMessage
    {
        public readonly ISlotActor Actor;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 ProjectileDirection;
        public readonly HitOutcome Outcome;
        public readonly DamageContext Context;

        public ActorHitMessage(
            ISlotActor actor,
            Vector3 worldPosition,
            Vector3 projectileDirection,
            HitOutcome outcome,
            DamageContext context = default)
        {
            Actor = actor;
            WorldPosition = worldPosition;
            ProjectileDirection = projectileDirection;
            Outcome = outcome;
            Context = context;
        }

        /// <summary>Evaluates the hit on the actor and constructs the message in one call.</summary>
        public static ActorHitMessage From(
            IHitable actor,
            Vector3 worldPosition,
            Vector3 projectileDirection,
            DamageContext context)
        {
            return new ActorHitMessage(
                (ISlotActor)actor,
                worldPosition,
                projectileDirection,
                actor.EvaluateHit(context),
                context);
        }
    }
}
