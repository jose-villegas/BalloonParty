using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    public class GridActorView : MonoBehaviour, IPoolable, ISlotActorView
    {
        [SerializeField] private TweenTracker _tweenTracker;

        public TweenTracker TweenTracker => _tweenTracker;
        public SlotActorKind ActorKind => SlotActorKind.Static;
        public Transform RotationPivot => transform;

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
