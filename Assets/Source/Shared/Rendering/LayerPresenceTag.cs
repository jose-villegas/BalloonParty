using UnityEngine;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>Counts its object in <see cref="LayerPresence" /> on <see cref="Layers" /> while it's active.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    internal sealed class LayerPresenceTag : MonoBehaviour
    {
        private int _layers;
        private bool _counted;

        internal int Layers
        {
            get => _layers;
            set
            {
                if (_counted)
                {
                    LayerPresence.Add(_layers, -1);
                    LayerPresence.Add(value, 1);
                }

                _layers = value;
            }
        }

        private void OnEnable()
        {
            LayerPresence.Add(_layers, 1);
            _counted = true;
        }

        private void OnDisable()
        {
            LayerPresence.Add(_layers, -1);
            _counted = false;
        }
    }
}
