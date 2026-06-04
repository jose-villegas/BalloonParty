using BalloonParty.Shared.Pool;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    [RequireComponent(typeof(SpriteRenderer))]
    internal class LeafSpriteView : MonoBehaviour, IPoolable
    {
        private SpriteRenderer _renderer;

        internal int PhyllotaxisIndex { get; set; }
        internal float DepthFactor { get; set; }
        internal Vector2 LeafDirection { get; set; }
        internal float BaseRotation { get; set; }

        public void OnSpawned()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            _renderer.enabled = true;
        }

        public void OnDespawned()
        {
            DOTween.Kill(transform);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;

            if (_renderer != null)
            {
                _renderer.enabled = false;
            }

            PhyllotaxisIndex = 0;
            DepthFactor = 0f;
            LeafDirection = Vector2.up;
            BaseRotation = 0f;
        }

        internal void Configure(Sprite sprite, int sortingLayerId, int sortingOrder)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            _renderer.sprite = sprite;
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
        }
    }
}

