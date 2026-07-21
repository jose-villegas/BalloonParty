using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Slots.Capabilities;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Balloon.Type
{
    public abstract class ColorableBalloonVariant : MonoBehaviour, IBalloonVariant
    {
        [SerializeField] [PaletteColorMask] private int _allowedColorsMask = ~0;

        [Inject] protected IGamePalette Palette;

        public virtual void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask)
        {
            if (model is IPaintable colorable)
            {
                colorable.Color.Value = PickColor(levelAllowedColorsMask) ?? "";
            }
        }

        // Empty intersection: fall back to the prefab mask rather than picking no color at all.
        private string PickColor(int levelAllowedColorsMask)
        {
            if (Palette == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}.PickColor: IGamePalette is null — DI not configured.");
            }

            if (Palette.Colors == null || Palette.Colors.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}.PickColor: IGamePalette has no colors configured.");
            }

            var mask = _allowedColorsMask & levelAllowedColorsMask;
            if (mask == 0)
            {
                Log.Warn("ColorableBalloon",
                    $"{GetType().Name}.PickColor: the active level's allowed-color gate has no " +
                    "overlap with this prefab's color mask — falling back to the prefab mask alone.");
                mask = _allowedColorsMask;
            }

            var allowedCount = CountAllowedColors(mask);
            if (allowedCount == 0)
            {
                Log.Error("ColorableBalloon",
                    $"{GetType().Name}.PickColor: allowed-colors mask excludes all palette colors.");
                return null;
            }

            return NthAllowedColor(mask, UnityEngine.Random.Range(0, allowedCount));
        }

        private int CountAllowedColors(int mask)
        {
            var count = 0;
            for (var i = 0; i < Palette.Colors.Count; i++)
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
            for (var i = 0; i < Palette.Colors.Count; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                if (current == target)
                {
                    return Palette.Colors[i].Name;
                }

                current++;
            }

            return null;
        }
    }
}
