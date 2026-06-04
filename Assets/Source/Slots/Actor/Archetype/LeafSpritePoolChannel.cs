using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    internal class LeafSpritePoolChannel : PoolChannel<LeafSpriteView>
    {
        private readonly LeafSpriteView _prefab;

        internal LeafSpritePoolChannel(LeafSpriteView prefab)
        {
            _prefab = prefab;
        }

        protected override LeafSpriteView Create()
        {
            return Object.Instantiate(_prefab);
        }
    }
}

