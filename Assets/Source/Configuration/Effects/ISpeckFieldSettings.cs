using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Tuning for the ambient GPU speck field, composed from cohesive groups (<see cref="Motion" />,
    /// <see cref="Appearance" />, <see cref="Spawning" />) so the inspector reads as foldouts rather than one
    /// long list. The <see cref="SpeckField" /> component keeps only its own rendering assets (compute shader +
    /// material); everything tunable lives here.</summary>
    internal interface ISpeckFieldSettings
    {
        int Count { get; }
        Vector2 RegionSize { get; }
        ISpeckMotionSettings Motion { get; }
        ISpeckAppearanceSettings Appearance { get; }
        ISpeckSpawnSettings Spawning { get; }
    }
}
