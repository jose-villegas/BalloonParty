#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using BalloonParty.Shared.Disturbance;
using UnityEngine;
using VContainer;

namespace BalloonParty.Cheats
{
    internal class DisturbanceStampCheat : MonoBehaviour, ICheat
    {
        [Inject] private DisturbanceFieldService _field;

        private bool _active;
        private Vector3 _lastMouseWorld;

        public string Name => _active ? "Stamp Disturbance  [ON]" : "Stamp Disturbance";
        public string Section => "Grid";
        public IReadOnlyList<string> Tags => new[] { "cloud", "disturbance", "grid" };

        private void Update()
        {
            if (!_active || _field == null)
            {
                return;
            }

            if (!Input.GetMouseButton(0))
            {
                _lastMouseWorld = Vector3.zero;
                return;
            }

            var pos = MouseWorldPosition();
            if (pos == null)
            {
                return;
            }

            var world = pos.Value;

            var direction = Vector2.zero;
            if (_lastMouseWorld != Vector3.zero)
            {
                var delta = world - _lastMouseWorld;
                direction = new Vector2(delta.x, delta.y).normalized;
            }

            _lastMouseWorld = world;

            _field.Stamp(world, 0.3f, 0.8f, direction);
        }

        public void Execute()
        {
            _active = !_active;
            _lastMouseWorld = Vector3.zero;
        }

        private static Vector3? MouseWorldPosition()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return null;
            }

            var world = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0f));
            world.z = 0f;
            return world;
        }
    }
}
#endif
