using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Plays an item's one-shot activation effect: pulls the <see cref="EffectView" /> for the
    ///     settings' prefab from the pool, tints it by the popped balloon's colour, and returns it on
    ///     completion. Shared by the handlers whose effect needs no per-instance preparation (bomb,
    ///     laser, shield); the chain/splash effects drive their own two-phase setup instead.
    /// </summary>
    internal class ItemEffectPlayer
    {
        private readonly PoolManager _poolManager;
        private readonly IGamePalette _palette;

        [Inject]
        public ItemEffectPlayer(PoolManager poolManager, IGamePalette palette)
        {
            _poolManager = poolManager;
            _palette = palette;
        }

        public void Play(ItemSettings settings, Vector3 worldPosition, string colorId)
        {
            if (!TryAcquire(settings, out var effect, out var key))
            {
                return;
            }

            effect.Play(worldPosition, _palette.GetColor(colorId), () => _poolManager.Return(key, effect));
        }

        public void Play(ItemSettings settings, Vector3 worldPosition, Quaternion rotation, string colorId)
        {
            if (!TryAcquire(settings, out var effect, out var key))
            {
                return;
            }

            effect.Play(worldPosition, rotation, _palette.GetColor(colorId), () => _poolManager.Return(key, effect));
        }

        private bool TryAcquire(ItemSettings settings, out EffectView effect, out string key)
        {
            effect = null;
            key = null;

            if (settings.ActivationEffectPrefab == null)
            {
                return false;
            }

            key = settings.ActivationEffectPrefab.name;
            effect = _poolManager.GetOrRegister(key, () => new SimplePoolChannel<EffectView>(settings.ActivationEffectPrefab));
            return true;
        }
    }
}
