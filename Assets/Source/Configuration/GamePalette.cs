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
            return _colors.First(c => c.Name == colorName).Color;
        }
    }
}


