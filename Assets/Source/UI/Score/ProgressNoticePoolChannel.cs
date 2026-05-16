using BalloonParty.Shared;
using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    internal class ProgressNoticePoolChannel : PoolChannel<ProgressNotice>
    {
        private readonly ProgressNotice _prefab;

        public ProgressNoticePoolChannel(ProgressNotice prefab)
        {
            _prefab = prefab;
        }

        protected override ProgressNotice Create()
        {
            var instance = Object.Instantiate(_prefab, Container);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}
