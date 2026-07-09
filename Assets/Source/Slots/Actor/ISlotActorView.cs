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
    }
}
