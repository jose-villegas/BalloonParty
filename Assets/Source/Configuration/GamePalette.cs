using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Game Palette", fileName = "GamePalette")]
    public class GamePalette : ScriptableObject, IGamePalette
    {
        [SerializeField] private PaletteEntry[] _colors;

        public IReadOnlyList<PaletteEntry> Colors => _colors;

        public Color GetColor(string colorName)
        {
            foreach (var c in _colors)
            {
                if (c.Name == colorName)
                {
                    return c.Color;
                }
            }

            throw new ArgumentException(
                $"GamePalette.GetColor: no palette entry found for color \"{colorName}\".");
        }
    }
}
