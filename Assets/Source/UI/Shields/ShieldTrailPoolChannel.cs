using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using UnityEngine;

namespace BalloonParty.UI.Shields
{
    public class ShieldTrailPoolChannel : PoolChannel<ScorePointTrail>
    {
        private readonly ScorePointTrail _prefab;

        public ShieldTrailPoolChannel(ScorePointTrail prefab)
        {
            _prefab = prefab;
        }

        protected override ScorePointTrail Create()
        {
            var instance = Object.Instantiate(_prefab, Container);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}

