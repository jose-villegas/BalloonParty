using System;
using System.Linq;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Game Palette", fileName = "GamePalette")]
    public class GamePalette : ScriptableObject
    {
        [SerializeField] private PaletteEntry[] _colors;

        public PaletteEntry[] Colors => _colors;

        public Color GetColor(string colorName)
        {
            var entry = _colors.FirstOrDefault(c => c.Name == colorName);
            if (entry == null)
            {
                throw new ArgumentException(
                    $"GamePalette.GetColor: no palette entry found for color \"{colorName}\".");
            }

            return entry.Color;
        }
    }
}
