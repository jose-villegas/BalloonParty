using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Configuration.Palette
{
    [CreateAssetMenu(menuName = "Configuration/Game Palette", fileName = "GamePalette")]
    public class GamePalette : ScriptableObject, IGamePalette
    {
        [SerializeField] private PaletteEntry[] _colors;

        private Dictionary<string, PaletteEntry> _byName;
        private string[] _names;

        public IReadOnlyList<PaletteEntry> Colors => _colors;
        public IReadOnlyList<string> ColorNames => _names ??= BuildNames();

        private void OnEnable()
        {
            _byName = null;
            _names = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _byName = null;
            _names = null;
        }
#endif

        public Color GetColor(string colorName)
        {
            return GetEntry(colorName)?.Color
                ?? throw new ArgumentException(
                    $"GamePalette.GetColor: no palette entry found for color \"{colorName}\".");
        }

        public PaletteEntry GetEntry(string colorName)
        {
            _byName ??= BuildLookup();
            return colorName != null && _byName.TryGetValue(colorName, out var entry) ? entry : null;
        }

        public IReadOnlyList<string> ColorNamesForMask(int mask)
        {
            var names = new List<string>();
            for (var i = 0; i < _colors.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    names.Add(_colors[i].Name);
                }
            }

            return names;
        }

        private Dictionary<string, PaletteEntry> BuildLookup()
        {
            var lookup = new Dictionary<string, PaletteEntry>(_colors.Length);
            foreach (var entry in _colors)
            {
                if (entry.Name != null && !lookup.ContainsKey(entry.Name))
                {
                    lookup.Add(entry.Name, entry);
                }
            }

            return lookup;
        }

        private string[] BuildNames()
        {
            var names = new string[_colors.Length];
            for (var i = 0; i < _colors.Length; i++)
            {
                names[i] = _colors[i].Name;
            }

            return names;
        }
    }
}
