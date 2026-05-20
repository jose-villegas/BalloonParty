using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Slots.StaticActor
{
    internal class StaticActorView : MonoBehaviour, IPoolable, ISlotActorView
    {
        [SerializeField] private TweenTracker _tweenTracker;

        public TweenTracker TweenTracker => _tweenTracker;
        public SlotActorKind ActorKind => SlotActorKind.Static;

        public void OnSpawned()
        {
            transform.localScale = Vector3.one;
        }

        public void OnDespawned()
        {
            _tweenTracker?.Kill();
        }
    }
}

