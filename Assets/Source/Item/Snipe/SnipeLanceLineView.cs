using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     One pencil stroke along a Snipe lance's run: a two-point <see cref="LineRenderer" /> carrying the
    ///     aim trace's smoke-and-glitter material, so the shot's path reads in the same carbon hand as the
    ///     cannon's aim. Driven entirely by <see cref="SnipeLanceLines" />; it only renders what it is told.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    internal sealed class SnipeLanceLineView : MonoBehaviour, IPoolable
    {
        [SerializeField] private LineRenderer _line;

        [Tooltip("Seconds the pencil tip takes to run from where the lance armed to the wall.")]
        [SerializeField] [Min(0f)] private float _drawSeconds = 0.18f;

        [Tooltip("Seconds the finished line stays at full strength.")]
        [SerializeField] [Min(0f)] private float _holdSeconds = 0.7f;

        [Tooltip("Seconds the line takes to fade out after the hold.")]
        [SerializeField] [Min(0f)] private float _fadeSeconds = 0.6f;

        private Vector3 _from;
        private Vector3 _to;
        private Color _color = Color.white;

        internal SnipeLanceStrokeTiming Timing => new(_drawSeconds, _holdSeconds, _fadeSeconds);

        private void Awake()
        {
            if (_line == null)
            {
                _line = GetComponent<LineRenderer>();
            }

            _line.useWorldSpace = true;
            _line.positionCount = 0;
        }

        public void OnSpawned()
        {
            _line.positionCount = 0;
        }

        public void OnDespawned()
        {
            _line.positionCount = 0;
        }

        internal void Begin(Vector3 from, Vector3 to, Color color)
        {
            _from = from;
            _to = to;
            _color = color;
            Render(0f, 1f);
        }

        /// <param name="drawn">How far the tip has run from the start toward the wall, 0 to 1.</param>
        /// <param name="alpha">The whole stroke's opacity, multiplied onto the colour's own.</param>
        internal void Render(float drawn, float alpha)
        {
            if (drawn <= 0f || alpha <= 0f)
            {
                _line.positionCount = 0;
                return;
            }

            var color = _color;
            color.a *= alpha;
            _line.startColor = color;
            _line.endColor = color;
            _line.positionCount = 2;
            _line.SetPosition(0, _from);
            _line.SetPosition(1, Vector3.Lerp(_from, _to, drawn));
        }
    }
}
