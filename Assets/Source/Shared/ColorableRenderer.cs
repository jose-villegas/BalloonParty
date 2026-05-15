using UnityEngine;

namespace BalloonParty.Shared
{
    public interface IColorableRenderer
    {
        void SetColor(Color color);
    }

    /// <summary>
    ///     Non-generic MonoBehaviour base that implements <see cref="IColorableRenderer" />.
    ///     Required so Unity can serialise <c>ColorableRenderer[]</c> fields — plain
    ///     <c>[SerializeField]</c> only works with <see cref="UnityEngine.Object" /> types,
    ///     not bare interfaces.
    /// </summary>
    public abstract class ColorableRenderer : MonoBehaviour, IColorableRenderer
    {
        public abstract void SetColor(Color color);
    }

    /// <summary>
    ///     Generic layer that fetches the required <typeparamref name="T" /> component
    ///     on <c>Awake</c>. Concrete subclasses only need <c>[RequireComponent]</c> and
    ///     a <c>SetColor</c> override.
    /// </summary>
    public abstract class ColorableRenderer<T> : ColorableRenderer
        where T : Component
    {
        private T _renderer;

        protected T Renderer => _renderer != null ? _renderer : (_renderer = GetComponent<T>());

        private void Awake()
        {
            _renderer = GetComponent<T>();
        }
    }
}
