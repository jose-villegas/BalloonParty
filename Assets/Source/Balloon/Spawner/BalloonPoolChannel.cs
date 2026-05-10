using BalloonParty.Balloon.View;
using BalloonParty.Shared;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Balloon.Spawner
{
    public class BalloonPoolChannel : PoolChannel<BalloonView>
    {
        private readonly GameObject _prefab;
        private readonly IObjectResolver _resolver;

        public BalloonPoolChannel(GameObject prefab, IObjectResolver resolver)
        {
            _prefab = prefab;
            _resolver = resolver;
        }

        protected override BalloonView Create()
        {
            var instance = _resolver.Instantiate(_prefab, Vector3.zero, Quaternion.identity);
            instance.transform.SetParent(Container);
            return instance.GetComponent<BalloonView>();
        }
    }
}
