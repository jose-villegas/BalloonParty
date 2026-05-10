#region

using BalloonParty.Shared;
using UnityEngine;

#endregion

namespace BalloonParty.UI.Score
{
    public class ScoreTrailPoolChannel : PoolChannel<ScorePointTrail>
    {
        private readonly ScorePointTrail _prefab;

        public ScoreTrailPoolChannel(ScorePointTrail prefab)
        {
            _prefab = prefab;
        }

        protected override ScorePointTrail Create()
        {
            var instance = Object.Instantiate(_prefab, Container);
            instance.Initialize(Return);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}
