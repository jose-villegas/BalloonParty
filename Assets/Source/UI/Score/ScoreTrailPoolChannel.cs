using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    internal class ScoreTrailPoolChannel : PoolChannel<FlyingTrail>
    {
        private readonly FlyingTrail _prefab;

        public ScoreTrailPoolChannel(FlyingTrail prefab)
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
