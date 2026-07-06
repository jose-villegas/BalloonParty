using System.Collections.Generic;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Authoring component for flattening rigid sprite layers into one baked sprite; data-only
    ///     at runtime, all bake logic lives in <c>SpriteLayerCombinerEditor</c>.
    /// </summary>
    internal sealed class SpriteLayerCombiner : MonoBehaviour
    {
        [Tooltip("Sprite layers to flatten, drawn by ascending sorting order. Must be " +
                 "rigid relative to each other (no independent animation).")]
        [SerializeField] private SpriteRenderer[] _layers;

        [Tooltip("Bake with renderer tints replaced by white — for layers recolored at " +
                 "runtime, where the combined renderer receives the tint instead. Off " +
                 "bakes the authored colors in (fixed overlays like shine/specular).")]
        [SerializeField] private bool _neutralizeTint = true;

        [Tooltip("Bakes at N× the sprites' pixels-per-unit.")]
        [SerializeField, Range(1, 8)] private int _resolutionMultiplier = 1;

        [Tooltip("Output file suffix, so one prefab can host several combiners " +
                 "(e.g. a tinted Body group and a fixed Shine group).")]
        [SerializeField] private string _outputSuffix = "Combined";

        internal IReadOnlyList<SpriteRenderer> Layers => _layers;
        internal bool NeutralizeTint => _neutralizeTint;
        internal int ResolutionMultiplier => _resolutionMultiplier;
        internal string OutputSuffix => _outputSuffix;
    }
}
