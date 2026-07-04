using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Authoring component for flattening rigid sprite layers into one baked sprite
    ///     (audit 5d — fewer draws and less overdraw per balloon): drop on the node that
    ///     will host the combined renderer, assign the layers, press <b>Bake</b> in the
    ///     inspector. The editor flattens them in sorting order into
    ///     <c>Assets/Sprites/Baked/Combined</c> (mirroring the prefab's path); wiring the
    ///     result into the prefab is manual. Layers recolored at runtime (via
    ///     <c>ColorableRenderer</c>) bake tint-neutral so the combined renderer takes the
    ///     runtime tint instead — only group layers that share one tint.
    ///     Batching note: combined sprites from different prefabs only batch together if
    ///     packed into one SpriteAtlas and drawn with one shared material.
    ///     Data-only at runtime; all bake logic lives in <c>SpriteLayerCombinerEditor</c>.
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
