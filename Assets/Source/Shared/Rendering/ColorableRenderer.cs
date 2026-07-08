using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    public interface IColorableRenderer
    {
        void SetColor(Color color);
    }

    /// <summary>
    ///     Non-generic base so Unity can serialise <c>ColorableRenderer[]</c> fields — <c>[SerializeField]</c>
    ///     doesn't work on bare interfaces.
    /// </summary>
    public abstract class ColorableRenderer : MonoBehaviour, IColorableRenderer
    {
        public abstract void SetColor(Color color);
    }

    /// <summary>
    ///     Fetches the required <typeparamref name="T" /> component on <c>Awake</c>.
    /// </summary>
    public abstract class ColorableRenderer<T> : ColorableRenderer
        where T : Component
    {
        [Tooltip("How strongly the passed colour is applied: <1 dims, 1 as-is, >1 overdrives (for bloom).")]
        [SerializeField] [Range(0f, 4f)] private float _colorIntensity = 1f;

        [Tooltip("Keep this renderer's own alpha instead of taking the passed colour's alpha.")]
        [SerializeField] private bool _preserveAlpha;

        private T _renderer;

        protected T Renderer => _renderer != null ? _renderer : _renderer = GetComponent<T>();

        private void Awake()
        {
            _renderer = GetComponent<T>();
        }

        // Scales the passed colour's RGB by the per-renderer intensity. Alpha comes from the passed
        // colour, or from this renderer's own alpha when _preserveAlpha is set. Leaves call this so both
        // knobs apply uniformly regardless of the concrete renderer type.
        protected Color WithIntensity(Color color)
        {
            var alpha = _preserveAlpha ? ReadAlpha() : color.a;
            return (color * _colorIntensity).WithAlpha(alpha);
        }

        // The renderer's current alpha — its authored value while _preserveAlpha keeps it untouched.
        protected abstract float ReadAlpha();
    }
}
