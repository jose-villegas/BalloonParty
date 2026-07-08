using System;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Plays an item's one-shot activation effect, pooled and tinted by the popped balloon's colour.
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

        /// <summary>
        ///     Plays the effect and returns its <see cref="EffectView.Duration" /> (0 if none). <paramref name="scale" />
        ///     multiplies the effect transform (VFX prefabs are authored at scale 1) and resets on completion.
        /// </summary>
        public float Play(ItemSettings settings, Vector3 worldPosition, string colorId, float scale = 1f)
        {
            if (!TryAcquire(settings, out var effect, out var key))
            {
                return 0f;
            }

            Action onComplete;
            if (Mathf.Approximately(scale, 1f))
            {
                onComplete = () => _poolManager.Return(key, effect);
            }
            else
            {
                effect.transform.localScale = Vector3.one * scale;
                onComplete = () =>
                {
                    effect.transform.localScale = Vector3.one;
                    _poolManager.Return(key, effect);
                };
            }

            effect.Play(worldPosition, _palette.GetColor(colorId), onComplete);
            return effect.Duration;
        }

        // Returns the played effect so the caller can post-configure it (e.g. a rainbow laser's colour
        // cycle); null when there's no prefab.
        public EffectView Play(ItemSettings settings, Vector3 worldPosition, Quaternion rotation, string colorId)
        {
            if (!TryAcquire(settings, out var effect, out var key))
            {
                return null;
            }

            effect.Play(worldPosition, rotation, _palette.GetColor(colorId), () => _poolManager.Return(key, effect));
            return effect;
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
