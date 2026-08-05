using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    public class GridActorView : MonoBehaviour, IPoolable, ISlotActorView
    {
        [SerializeField] private Collider2D _collider;
        [SerializeField] private TweenTracker _tweenTracker;

        public TweenTracker TweenTracker => _tweenTracker;
        public SlotActorKind ActorKind => SlotActorKind.Static;
        public Transform RotationPivot => transform;

        // Feeds the shot solver's Phase A static-archetype collision geometry (Deflector/Gatekeeper/
        // Absorber) — zero (collision-inert) until a collider is authored on the prefab.
        public float ContactRadius => BalloonParty.Shared.ContactRadius.FromCollider(_collider, transform.lossyScale.x);

        public Vector3 ContactCenter =>
            _collider != null ? transform.TransformPoint(_collider.offset) : transform.position;

        public bool HasActiveCollider => _collider != null && _collider.enabled;

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
