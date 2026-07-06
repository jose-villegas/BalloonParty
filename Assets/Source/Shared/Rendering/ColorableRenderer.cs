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
        private T _renderer;

        protected T Renderer => _renderer != null ? _renderer : _renderer = GetComponent<T>();

        private void Awake()
        {
            _renderer = GetComponent<T>();
        }
    }
}
