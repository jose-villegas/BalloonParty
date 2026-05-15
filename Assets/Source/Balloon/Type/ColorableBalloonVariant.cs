using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    public abstract class ColorableBalloonVariant : MonoBehaviour, IBalloonVariant
    {
        [SerializeField] [PaletteColorMask] private int _allowedColorsMask = ~0;

        [Inject] private GamePalette _palette;

        public virtual void Initialize(IWriteableBalloonModel model)
        {
            model.Color.Value = PickColor() ?? "";
        }

        private string PickColor()
        {
            if (_palette == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}.PickColor: GamePalette is null — DI not configured.");
            }

            if (_palette.Colors == null || _palette.Colors.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}.PickColor: GamePalette has no colors configured.");
            }

            var colors = _palette.Colors;
            var count = 0;

            for (var i = 0; i < colors.Length; i++)
            {
                if ((_allowedColorsMask & (1 << i)) != 0)
                {
                    count++;
                }
            }

            if (count == 0)
            {
                Debug.LogError(
                    $"{GetType().Name}.PickColor: allowed-colors mask excludes all palette colors.");
                return null;
            }

            var pick = UnityEngine.Random.Range(0, count);
            var current = 0;

            for (var i = 0; i < colors.Length; i++)
            {
                if ((_allowedColorsMask & (1 << i)) == 0)
                {
                    continue;
                }

                if (current == pick)
                {
                    return colors[i].Name;
                }

                current++;
            }

            return null;
        }
    }
}
