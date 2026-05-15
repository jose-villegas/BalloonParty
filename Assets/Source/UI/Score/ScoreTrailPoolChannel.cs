using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    internal class ScoreTrailPoolChannel : PoolChannel<ScorePointTrail>
    {
        private readonly ScorePointTrail _prefab;

        public ScoreTrailPoolChannel(ScorePointTrail prefab)
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
