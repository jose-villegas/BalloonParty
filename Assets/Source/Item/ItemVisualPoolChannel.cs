#region

using BalloonParty.Shared;
using UnityEngine;

#endregion

namespace BalloonParty.Item
{
    /// <summary>
    ///     One pool channel per item visual prefab. Keyed by prefab name in <see cref="PoolManager" />.
    ///     Instances are parented to the balloon's Display child when in use, then returned here on release.
    /// </summary>
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

