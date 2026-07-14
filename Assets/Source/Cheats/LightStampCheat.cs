#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections.Generic;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer;
// Disambiguate from UnityEngine.Light — both are in scope here (see the SceneLight README).
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Places lights into <see cref="SceneLightFieldService"/> to eyeball the field. Palette is chosen
    ///     from inline buttons (works on touch — no right/middle-click), then a tap on the board turns a
    ///     light on there; each stays until "Clear" turns them all off. Demonstrates the caller-owned
    ///     on/off lifecycle: a registration is disposed to remove its light.
    /// </summary>
    internal class LightStampCheat : MonoBehaviour, ICheat, ICheatControls
    {
        private const int PaletteCycle = 8;
        private const float LightRadius = 1.0f;
        private const float LightIntensity = 1.0f;

        [Inject] private SceneLightFieldService _field;

        private readonly List<IDisposable> _placed = new();

        private bool _active;
        private int _paletteIndex;

        public string Name => _active ? $"Place Light  [ON · p{_paletteIndex} · {_placed.Count}]" : "Place Light";
        public string Section => "Lighting";
        public IReadOnlyList<string> Tags => new[] { "light", "field", "lighting" };

        private void Update()
        {
            if (!_active || _field == null || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            var world = CheatInput.MouseWorldPosition();
            if (world == null)
            {
                return;
            }

            var light = new Light(world.Value, LightRadius, LightIntensity, _paletteIndex);
            _placed.Add(_field.RegisterLight(light));
        }

        private void OnDestroy()
        {
            Clear();
        }

        public void Execute()
        {
            _active = !_active;
        }

        public void DrawControls()
        {
            GUILayout.BeginHorizontal();
            _active = GUILayout.Toggle(_active, _active ? "Placing" : "Place", "Button", GUILayout.Width(80));
            if (GUILayout.Button($"Clear ({_placed.Count})"))
            {
                Clear();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Palette", GUILayout.Width(56));
            if (GUILayout.Button("−", GUILayout.Width(28)))
            {
                _paletteIndex = (_paletteIndex + PaletteCycle - 1) % PaletteCycle;
            }

            GUILayout.Label(_paletteIndex.ToString(), GUILayout.Width(24));
            if (GUILayout.Button("+", GUILayout.Width(28)))
            {
                _paletteIndex = (_paletteIndex + 1) % PaletteCycle;
            }

            GUILayout.EndHorizontal();
        }

        private void Clear()
        {
            foreach (var registration in _placed)
            {
                registration.Dispose();
            }

            _placed.Clear();
        }
    }
}
#endif
