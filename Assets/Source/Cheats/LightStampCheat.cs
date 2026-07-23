#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer;
// Disambiguate from UnityEngine.Light — both are in scope here (see the SceneLight README).
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Places lights into <see cref="SceneLightFieldService"/> to eyeball the field. Palette is chosen
    ///     from a name dropdown (works on touch — no right/middle-click), then a tap on the board turns a
    ///     light on there; each stays until "Clear" turns them all off. Demonstrates the caller-owned
    ///     on/off lifecycle: a registration is disposed to remove its light.
    /// </summary>
    internal class LightStampCheat : MonoBehaviour, ICheat, ICheatControls
    {
        private const float LightRadius = 1.0f;
        private const float LightIntensity = 1.0f;

        [Inject] private SceneLightFieldService _field;
        [Inject] private IGamePalette _palette;

        private readonly List<IDisposable> _placed = new();

        private bool _active;
        private int _paletteIndex;

        public string Name => _active ? $"Place Light  [ON · {PaletteName()} · {_placed.Count}]" : "Place Light";
        public string Section => "Lighting";
        public IReadOnlyList<string> Tags => new[] { "light", "field", "lighting" };
        public bool Compact => false;

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

            var names = _palette?.ColorNames;
            if (names == null || names.Count == 0)
            {
                GUILayout.Label("Palette unavailable");
                return;
            }

            _paletteIndex = Mathf.Clamp(_paletteIndex, 0, names.Count - 1);
            _paletteIndex = CheatLayout.Dropdown("light.palette", names.ToArray(), _paletteIndex);
        }

        private void Clear()
        {
            foreach (var registration in _placed)
            {
                registration.Dispose();
            }

            _placed.Clear();
        }

        // _paletteIndex is a raw index into IGamePalette.Colors (see SceneLightFieldService), so the
        // display name has to come from the same list.
        private string PaletteName()
        {
            var names = _palette?.ColorNames;
            return names != null && _paletteIndex >= 0 && _paletteIndex < names.Count ? names[_paletteIndex] : "?";
        }
    }
}
#endif
