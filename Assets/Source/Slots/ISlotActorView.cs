using BalloonParty.Shared.Animation;
using UnityEngine;

namespace BalloonParty.Slots
{
    public interface ISlotActorView
    {
        Transform transform { get; }
        TweenTracker TweenTracker { get; }
        SlotActorKind ActorKind { get; }
    }
}

