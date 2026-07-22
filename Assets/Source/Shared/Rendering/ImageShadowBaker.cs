using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Authoring component for baked drop shadows on UI <see cref="Image"/> hierarchies;
    ///     data-only at runtime, all bake logic lives in <c>ImageShadowBakerEditor</c>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    internal sealed class ImageShadowBaker : MonoBehaviour
    {
        [Header("Shadow")]
        [SerializeField] private Color _shadowColor = new(0.2f, 0.2f, 0.2f, 0.75f);

        [Tooltip("Local offset of the shadow child from this RectTransform (in UI units).")]
        [SerializeField] private Vector2 _shadowOffset = new(2f, -2f);

        [Tooltip("Blur radius in UI units — baked offline, so generous values cost nothing at runtime.")]
        [SerializeField, Range(0f, 32f)] private float _blurRadius = 4f;

        [Header("Bake quality")]
        [Tooltip("Bakes at N× resolution for a crisper penumbra.")]
        [SerializeField, Range(1, 4)] private int _resolutionMultiplier = 2;

        [Tooltip("Iterated box blur passes — 3 approximates a Gaussian.")]
        [SerializeField, Range(1, 4)] private int _blurPasses = 3;

        [SerializeField, HideInInspector] private Image _shadowChild;

        internal Color ShadowColor => _shadowColor;
        internal Vector2 ShadowOffset => _shadowOffset;
        internal float BlurRadius => _blurRadius;
        internal int ResolutionMultiplier => _resolutionMultiplier;
        internal int BlurPasses => _blurPasses;

        internal Image ShadowChild
        {
            get => _shadowChild;
            set => _shadowChild = value;
        }
    }
}
