#region

using BalloonParty.Shared;
using UnityEngine;

#endregion

namespace BalloonParty.Item
{
    public class ItemVisualPoolChannel : PoolChannel<ItemVisualView>
    {
        private readonly GameObject _prefab;

        public ItemVisualPoolChannel(GameObject prefab)
        {
            _prefab = prefab;
        }

        protected override ItemVisualView Create()
        {
            var go = Object.Instantiate(_prefab, Container);
            var view = go.GetComponent<ItemVisualView>();
            go.SetActive(false);
            return view;
        }
    }
}
