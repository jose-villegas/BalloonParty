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
    }
}
