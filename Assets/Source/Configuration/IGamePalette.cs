using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    public interface IGamePalette
    {
        IReadOnlyList<PaletteEntry> Colors { get; }
        Color GetColor(string colorName);
    }
}


