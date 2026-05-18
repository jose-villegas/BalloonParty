using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using UnityEngine;

namespace BalloonParty.UI.Shields
{
    internal class ShieldTrailPoolChannel : PoolChannel<FlyingTrail>
    {
        private readonly FlyingTrail _prefab;

        public ShieldTrailPoolChannel(FlyingTrail prefab)
        {
            _prefab = prefab;
        }

        protected override FlyingTrail Create()
        {
            var instance = Object.Instantiate(_prefab, Container);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}
