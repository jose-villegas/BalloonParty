using Object = UnityEngine.Object;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Creates <see cref="EffectView" /> instances from a prefab that already has
    ///     a concrete <see cref="EffectView" /> subclass attached
    ///     (<see cref="ParticleEffectView" /> or <see cref="AnimatorEffectView" />).
    /// </summary>
    public class EffectPoolChannel : PoolChannel<EffectView>
    {
        private readonly EffectView _prefab;

        public EffectPoolChannel(EffectView prefab)
        {
            _prefab = prefab;
        }

        protected override EffectView Create()
        {
            var instance = Object.Instantiate(_prefab, Container);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}
