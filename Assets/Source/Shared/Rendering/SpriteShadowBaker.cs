using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Authoring component for baked drop shadows: drop on a prefab, tune, press <b>Bake</b>
    ///     in the inspector. The editor bakes the blurred union silhouette of every child sprite
    ///     under this transform into <c>Assets/Sprites/Baked/Shadows</c> (mirroring the prefab's
    ///     path and name), optionally swaps the runtime shadow/blur materials for a plain sprite
    ///     material — adjusting renderer sizes to absorb the shader's <c>_SpriteScale</c> — and
    ///     wires a shadow child that displays the baked sprite. Data-only at runtime; all bake
    ///     logic lives in <c>SpriteShadowBakerEditor</c>.
    /// </summary>
    internal sealed class SpriteShadowBaker : MonoBehaviour
    {
        [Header("Shadow")]
        [SerializeField] private Color _shadowColor = new(0.2f, 0.2f, 0.2f, 0.75f);

        [Tooltip("World-space offset of the shadow child from this transform.")]
        [SerializeField] private Vector2 _shadowOffset = new(0.04f, -0.04f);

        [Tooltip("Blur radius in world units — baked offline, so generous values cost nothing at runtime.")]
        [SerializeField, Range(0f, 0.5f)] private float _blurRadius = 0.06f;

        [Header("Bake quality")]
        [Tooltip("Bakes at N× the sprites' pixels-per-unit for a crisper penumbra.")]
        [SerializeField, Range(1, 8)] private int _resolutionMultiplier = 2;

        [Tooltip("Iterated box blur passes — 3 approximates a Gaussian.")]
        [SerializeField, Range(1, 4)] private int _blurPasses = 3;

        [Header("Material swap")]
        [Tooltip("Swap SpriteShadow/SpriteBlur family materials on child sprites for a plain one, " +
                 "shrinking each renderer by the material's _SpriteScale so the visible size is unchanged.")]
        [SerializeField] private bool _replaceShadowMaterials = true;

        [Tooltip("Material to swap in. Leave empty for Sprites-Default.")]
        [SerializeField] private Material _replacementMaterial;

        [SerializeField, HideInInspector] private SpriteRenderer _shadowChild;

        internal Color ShadowColor => _shadowColor;
        internal Vector2 ShadowOffset => _shadowOffset;
        internal float BlurRadius => _blurRadius;
        internal int ResolutionMultiplier => _resolutionMultiplier;
        internal int BlurPasses => _blurPasses;
        internal bool ReplaceShadowMaterials => _replaceShadowMaterials;
        internal Material ReplacementMaterial => _replacementMaterial;

        internal SpriteRenderer ShadowChild
        {
            get => _shadowChild;
            set => _shadowChild = value;
        }
    }
}
