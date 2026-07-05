using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Slots.Capabilities;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    public abstract class ColorableBalloonVariant : MonoBehaviour, IBalloonVariant
    {
        [SerializeField] [PaletteColorMask] private int _allowedColorsMask = ~0;

        [Inject] private IGamePalette _palette;

        public virtual void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask)
        {
            if (model is IPaintable colorable)
            {
                colorable.Color.Value = PickColor(levelAllowedColorsMask) ?? "";
            }
        }

        // Prefab mask = this skin's static color capability; level mask = the active range's
        // gate. Their intersection is what's actually pickable this level. An empty intersection
        // means the level gate excludes every color this prefab can be — falling back to the
        // prefab mask alone keeps the balloon paintable rather than picking no color at all.
        private string PickColor(int levelAllowedColorsMask)
        {
            if (_palette == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}.PickColor: IGamePalette is null — DI not configured.");
            }

            if (_palette.Colors == null || _palette.Colors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}.PickColor: IGamePalette has no colors configured.");
            }

            var mask = _allowedColorsMask & levelAllowedColorsMask;
            if (mask == 0)
            {
                Debug.LogWarning(
                    $"{GetType().Name}.PickColor: the active level's allowed-color gate has no " +
                    "overlap with this prefab's color mask — falling back to the prefab mask alone.");
                mask = _allowedColorsMask;
            }

            var allowedCount = CountAllowedColors(mask);
            if (allowedCount == 0)
            {
                Debug.LogError(
                    $"{GetType().Name}.PickColor: allowed-colors mask excludes all palette colors.");
                return null;
            }

            return NthAllowedColor(mask, UnityEngine.Random.Range(0, allowedCount));
        }

        private int CountAllowedColors(int mask)
        {
            var count = 0;
            for (var i = 0; i < _palette.Colors.Count; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private string NthAllowedColor(int mask, int target)
        {
            var current = 0;
            for (var i = 0; i < _palette.Colors.Count; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                if (current == target)
                {
                    return _palette.Colors[i].Name;
                }

                current++;
            }

            return null;
        }
    }
}
