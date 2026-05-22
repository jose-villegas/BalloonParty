using BalloonParty.Shared.Animation;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    public interface ISlotActorView
    {
        Transform transform { get; }
        TweenTracker TweenTracker { get; }
        SlotActorKind ActorKind { get; }
    }
}
