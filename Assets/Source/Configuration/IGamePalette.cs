using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    public interface IGamePalette
    {
        IReadOnlyList<PaletteEntry> Colors { get; }
        IReadOnlyList<string> ColorNames { get; }
        Color GetColor(string colorName);

        /// <summary>
        ///     The entry for <paramref name="colorName" />, or <c>null</c> if none matches. Use when a
        ///     missing color is a valid case (e.g. editor validation); <see cref="GetColor" /> throws.
        /// </summary>
        PaletteEntry GetEntry(string colorName);

        /// <summary>Color names whose bit is set in <paramref name="mask" /> — bit <c>i</c> is
        /// <see cref="Colors" />[<c>i</c>], same convention as <see cref="PaletteColorMaskAttribute" />.</summary>
        IReadOnlyList<string> ColorNamesForMask(int mask);
    }
}
