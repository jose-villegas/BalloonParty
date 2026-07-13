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
        /// <summary>
        ///     Reserved color id for the "all colors" wildcard (the rainbow balloon). Never a palette
        ///     entry — a balloon carrying this has no concrete color, so colour interactions can detect
        ///     it via <see cref="IsRainbow" /> instead of acting on an arbitrary spawn colour.
        /// </summary>
        public const string RainbowColorId = "__rainbow__";

        /// <summary>
        ///     Presentation-only palette entry for tough/heavy impacts (deflect stamps, tough pops) —
        ///     authored in the palette asset but never a spawnable balloon color.
        /// </summary>
        public const string ToughColorId = "Tough";

        /// <summary>
        ///     Presentation-only palette entry for the unbreakable's metallic impacts (its deflects and
        ///     pierced pop) — authored in the palette asset but never a spawnable balloon color.
        /// </summary>
        public const string SparksColorId = "Sparks";

        /// <summary>
        ///     Presentation-only palette entry for the unbreakable's constant field pulse — authored in
        ///     the palette asset but never a spawnable balloon color.
        /// </summary>
        public const string UnbreakableColorId = "Unbreakable";

        [SerializeField] private PaletteEntry[] _colors;

        private Dictionary<string, PaletteEntry> _byName;
        private string[] _names;
        private string[] _progressNames;

        public IReadOnlyList<PaletteEntry> Colors => _colors;
        public IReadOnlyList<string> ColorNames => _names ??= BuildNames();
        public IReadOnlyList<string> ProgressColorNames => _progressNames ??= BuildProgressNames();

        private void OnEnable()
        {
            _byName = null;
            _names = null;
            _progressNames = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _byName = null;
            _names = null;
            _progressNames = null;
        }
#endif

        public Color GetColor(string colorName)
        {
            // The wildcard has no concrete colour; white is a neutral tint for consumers that don't
            // (yet) special-case rainbow via IsRainbow.
            if (colorName == RainbowColorId)
            {
                return Color.white;
            }

            return GetEntry(colorName)?.Color
                ?? throw new ArgumentException(
                    $"GamePalette.GetColor: no palette entry found for color \"{colorName}\".");
        }

        public bool IsRainbow(string colorId)
        {
            return colorId == RainbowColorId;
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

        private string[] BuildProgressNames()
        {
            var names = new List<string>(_colors.Length);
            foreach (var entry in _colors)
            {
                if (entry.IsProgress)
                {
                    names.Add(entry.Name);
                }
            }

            return names.ToArray();
        }
    }
}
