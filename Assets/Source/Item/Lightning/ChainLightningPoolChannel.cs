using BalloonParty.Shared;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Creates <see cref="ChainLightningView" /> instances from a prefab.
    ///     Register once via <see cref="PoolManager.GetOrRegister" />.
    /// </summary>
    public class ChainLightningPoolChannel : PoolChannel<ChainLightningView>
    {
        private readonly GameObject _prefab;

        public ChainLightningPoolChannel(GameObject prefab)
        {
            _prefab = prefab;
        }

        protected override ChainLightningView Create()
        {
            var go = Object.Instantiate(_prefab, Container);
            return go.GetComponent<ChainLightningView>();
        }
    }
}

