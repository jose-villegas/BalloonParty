#region

using BalloonParty.Shared;
using UnityEngine;

#endregion

namespace BalloonParty.UI.Score
{
    public class ScoreNoticePoolChannel : PoolChannel<ScoreNotice>
    {
        private readonly ScoreNotice _prefab;

        public ScoreNoticePoolChannel(ScoreNotice prefab)
        {
            _prefab = prefab;
        }

        protected override ScoreNotice Create()
        {
            var instance = Object.Instantiate(_prefab, Container);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}

