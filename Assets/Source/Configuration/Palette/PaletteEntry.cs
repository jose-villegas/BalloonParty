using System;
using UnityEngine;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Configuration.Palette
{
    [Serializable]
    public class PaletteEntry
    {
        [SerializeField] private string _name;
        [SerializeField] private Color _color;

        public string Name => _name;
        public Color Color => _color;
    }
}
