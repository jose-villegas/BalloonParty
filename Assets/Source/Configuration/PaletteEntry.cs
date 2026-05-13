using System;
using UnityEngine;

namespace BalloonParty.Configuration
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

