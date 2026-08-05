using BalloonParty.Shared.Animation;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    public interface ISlotActorView
    {
        Transform transform { get; }
        TweenTracker TweenTracker { get; }
        SlotActorKind ActorKind { get; }

        /// <summary>
        ///     Transform an effect should rotate when tilting the actor, so lighting-baked children (e.g.
        ///     specular fakes) parented outside it keep a consistent light direction. Defaults to
        ///     <see cref="transform" /> for actors with nothing to protect.
        /// </summary>
        Transform RotationPivot { get; }

        /// <summary>
        ///     World radius of the circle a shot collides with; 0 when the actor is collision-inert.
        /// </summary>
        float ContactRadius { get; }

        /// <summary>
        ///     World centre of that circle — the collider, not the pivot. A shot reflects off where the
        ///     collider is, and the two differ whenever one is authored with an offset.
        /// </summary>
        Vector3 ContactCenter { get; }

        /// <summary>False while a pooled view is mid-despawn: its collider is off before it returns.</summary>
        bool HasActiveCollider { get; }
    }
}
