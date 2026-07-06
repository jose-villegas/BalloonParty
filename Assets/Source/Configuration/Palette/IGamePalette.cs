using System.Collections.Generic;
using UnityEngine;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Configuration.Palette
{
    public interface IGamePalette
    {
        IReadOnlyList<PaletteEntry> Colors { get; }
        IReadOnlyList<string> ColorNames { get; }
        Color GetColor(string colorName);

        /// <summary>
        ///     Returns <c>null</c> on no match, unlike <see cref="GetColor" />, which throws.
        /// </summary>
        PaletteEntry GetEntry(string colorName);

        /// <summary>Color names whose bit is set in <paramref name="mask" />, per <see cref="PaletteColorMaskAttribute" />'s convention.</summary>
        IReadOnlyList<string> ColorNamesForMask(int mask);
    }
}
