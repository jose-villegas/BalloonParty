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

        [Tooltip("Presentation-only tint (impact/pulse colors like Tough/Sparks/Unbreakable) — never " +
                 "spawned or scored, so it's excluded from the progress colors.")]
        [SerializeField] private bool _presentationOnly;

        public string Name => _name;
        public Color Color => _color;

        /// <summary>A spawnable/scorable color that counts toward level progress (the default).</summary>
        public bool IsProgress => !_presentationOnly;
    }
}
